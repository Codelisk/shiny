﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.Bluetooth.LE;
using Android.OS;
using Java.Util;
using Shiny.BluetoothLE.Hosting.Internals;
using static Android.Manifest;

namespace Shiny.BluetoothLE.Hosting;


public partial class BleHostingManager : IBleHostingManager
{
    readonly Dictionary<string, GattService> services = new();
    readonly GattServerContext context;
    AdvertisementCallbacks? adCallbacks;


    public async Task<L2CapInstance> OpenL2Cap(bool secure, Action<L2CapChannel> onOpen)
    {
        (await this.RequestAccess()).Assert();

        var ct = new CancellationTokenSource();
        var ad = this.context.Platform.GetBluetoothAdapter();

        if (ad == null)
            throw new InvalidOperationException("No Bluetooth Adaptor found");

        var serverSocket = secure
            ? ad.ListenUsingL2capChannel()
            : ad.ListenUsingInsecureL2capChannel();

        var psm = Convert.ToUInt16(serverSocket!.Psm);

        // TODO: error trap
        _ = Task.Run(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var socket = serverSocket.Accept(30000);
                    if (socket != null && !ct.IsCancellationRequested)
                    {
                        onOpen(new L2CapChannel(
                            psm,
                            socket.InputStream!,
                            socket.OutputStream!
                        ));
                    }
                }
                catch { }
            }
        });

        return new L2CapInstance(
            psm,
            () =>
            {
                ct.Cancel();
                serverSocket?.Dispose();
            }
        );
    }


    public async Task<AccessState> RequestAccess(bool advertise = true, bool connect = true)
    {
        if (!advertise && !connect)
            throw new ArgumentException("You must request at least 1 permission");

        if (!OperatingSystemShim.IsAndroidVersionAtLeast(23))
            return AccessState.NotSupported; //throw new InvalidOperationException("BLE Advertiser needs API Level 23+");

        var current = this.context.Manager.GetAccessState();
        if (current != AccessState.Available && current != AccessState.Unknown)
            return current;

        if (OperatingSystemShim.IsAndroidVersionAtLeast(31))
        {
            var perms = new List<string>();
            if (advertise)
                perms.Add(Permission.BluetoothAdvertise);

            if (connect)
                perms.Add(Permission.BluetoothConnect);

            var result = await this.context.Platform.RequestPermissions(perms.ToArray());
            if (!result.IsSuccess())
                return AccessState.Denied;
        }
        return AccessState.Available;
    }


    public bool IsAdvertising => this.adCallbacks != null;
    public IReadOnlyList<IGattService> Services => this.services.Values.Cast<IGattService>().ToArray();


    public async Task<IGattService> AddService(string uuid, bool primary, Action<IGattServiceBuilder> serviceBuilder)
    {
        (await this.RequestAccess(false, true)).Assert();

        var service = new GattService(this.context, uuid, primary);
        serviceBuilder(service);

        //var task = this.context
        //    .WhenServiceAdded
        //    .Take(1)
        //    .Timeout(TimeSpan.FromSeconds(5))
        //    .ToTask();

        if (!this.context.Server.AddService(service.Native))
            throw new InvalidOperationException("Service operation did not complete - look at logs");

        //await task.ConfigureAwait(false);
        this.services.Add(uuid, service);
        return service;
    }


    public void ClearServices()
    {
        this.services.Clear();
        this.context.Server.ClearServices();
        this.Cleanup();
    }


    public void RemoveService(string serviceUuid)
    {
        var uuid = UUID.FromString(serviceUuid);
        var s = this.services.ContainsKey(serviceUuid)
            ? this.services[serviceUuid].Native
            : this.context.Server.Services?.FirstOrDefault(x => x.Uuid?.Equals(uuid) ?? false);

        if (s != null)
            this.context.Server.RemoveService(s);

        this.services.Remove(serviceUuid);
        if (this.services.Count == 0)
            this.Cleanup();
    }


    public async Task StartAdvertising(AdvertisementOptions? options = null)
    {
        options ??= new AdvertisementOptions();

        var settings = new AdvertiseSettings.Builder()!
            .SetAdvertiseMode(AdvertiseMode.Balanced)!
            .SetConnectable(true)!;

        var data = new AdvertiseData.Builder()!;
        foreach (var uuid in options.ServiceUuids)
        {
            var nativeUuid = UUID.FromString(uuid);
            var parcel = new ParcelUuid(nativeUuid);
            data = data!.AddServiceUuid(parcel);
        }

        await this.DoAdvertise(settings, data);

        if (options.LocalName != null)
        {
            var adapter = this.context.Platform.GetBluetoothAdapter()!;
            adapter.SetName(options.LocalName); // TODO: verify name length with exception
        }
    }


    public void StopAdvertising()
    {
        var adapter = this.context.Platform.GetBluetoothAdapter()!;
        adapter.BluetoothLeAdvertiser!.StopAdvertising(this.adCallbacks);
        this.adCallbacks = null;
    }


    public Task AdvertiseBeacon(Guid uuid, ushort major, ushort minor, sbyte? txpower = null)
    {
        //https://www.pubnub.com/blog/build-android-ibeacon-beacon-emitter/
        var settings = new AdvertiseSettings.Builder()!
            .SetAdvertiseMode(AdvertiseMode.LowPower)!
            .SetTimeout(0)!
            .SetTxPowerLevel(AdvertiseTx.PowerMedium)!
            .SetConnectable(false)!;

        var data = new AdvertiseData.Builder()!;
        //.SetIncludeTxPowerLevel(options.AndroidIncludeTxPower);

        var bytes = new List<byte>();

        // beacon identifier
        bytes.Add(0xBE);
        bytes.Add(0xAC);

        // beacon data
        bytes.AddRange(uuid.ToByteArray());
        bytes.AddRange(BitConverter.GetBytes(major));
        bytes.AddRange(BitConverter.GetBytes(minor));
        bytes.Add((byte)(txpower ?? -75));

        // 224 - Google's Manufacturer ID
        // 79 - Apple's manufacturer ID
        data.AddManufacturerData(79, bytes.ToArray());

        return this.DoAdvertise(settings, data);
    }


    void Cleanup()
    {
        foreach (var service in this.services)
            service.Value.Dispose();

        this.services.Clear();
        this.context.CloseServer();
    }


    async Task DoAdvertise(AdvertiseSettings.Builder settings, AdvertiseData.Builder data)
    {
        (await this.RequestAccess(true, false)).Assert();

        var tcs = new TaskCompletionSource<bool>();
        this.adCallbacks = new AdvertisementCallbacks(
            () => tcs.SetResult(true),
            ex => tcs.SetException(ex)
        );
        var adapter = this.context.Platform.GetBluetoothAdapter()!;
        adapter.BluetoothLeAdvertiser!.StartAdvertising(
            settings.Build(),
            data.Build(),
            this.adCallbacks
        );

        await tcs.Task.ConfigureAwait(false);
    }
}