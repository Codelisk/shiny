﻿using System.Reactive.Linq;
using System.Text;

namespace Shiny.BluetoothLE;


public static class CommonBleExtensions
{
    public static void Respond<T>(this IObserver<T> obs, T result)
    {
        obs.OnNext(result);
        obs.OnCompleted();
    }


    public static IObservable<DeviceInfo> ReadDeviceInformation(this IPeripheral peripheral)
        => peripheral
            .GetKnownService(StandardUuids.DeviceInformationServiceUuid, true)
            .SelectMany(x => x.GetCharacteristics())
            .SelectMany(x => x.Select(y => y.Read()))
            .Concat()
            .ToList()
            .Select(data =>
            {
                var dev = new DeviceInfo();
                foreach (var item in data)
                {
                    switch (item.Characteristic.Uuid.ToLower())
                    {
                        case "00002a23-0000-1000-8000-00805f9b34fb":
                            dev.SystemId = Encoding.UTF8.GetString(item.Data);
                            break;

                        case "00002a24-0000-1000-8000-00805f9b34fb":
                            dev.ModelNumber = Encoding.UTF8.GetString(item.Data);
                            break;

                        case "00002a25-0000-1000-8000-00805f9b34fb":
                            dev.SerialNumber = Encoding.UTF8.GetString(item.Data);
                            break;

                        case "00002a26-0000-1000-8000-00805f9b34fb":
                            dev.FirmwareRevision = Encoding.UTF8.GetString(item.Data);
                            break;

                        case "00002a27-0000-1000-8000-00805f9b34fb":
                            dev.HardwareRevision = Encoding.UTF8.GetString(item.Data);
                            break;

                        case "00002a28-0000-1000-8000-00805f9b34fb":
                            dev.SoftwareRevision = Encoding.UTF8.GetString(item.Data);
                            break;

                        case "00002a29-0000-1000-8000-00805f9b34fb":
                            dev.ManufacturerName = Encoding.UTF8.GetString(item.Data);
                            break;
                    }
                }
                return dev;
            });


    public static IObservable<int> ReadBatteryInformation(this IPeripheral peripheral)
        => StandardIntObserable(peripheral, StandardUuids.BatteryService);

    public static IObservable<int> HeartRateSensor(this IPeripheral peripheral)
        => StandardIntObserable(peripheral, StandardUuids.HeartRateMeasurementSensor);


    static IObservable<int> StandardIntObserable(IPeripheral peripheral, (string ServiceUuid, string CharacteristicUuid) uuid) => peripheral
        .GetKnownCharacteristic(uuid.ServiceUuid, uuid.CharacteristicUuid, true)
        .Select(x => x.Read())
        .Switch()
        .Select(x => (int)x.Data[0]);
}
