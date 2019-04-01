﻿using System;
using System.Reactive.Linq;
using System.Windows.Input;
using Humanizer;
using ReactiveUI;
using Shiny;
using Shiny.Net.Http;


namespace Samples.HttpTransfers
{
    public class HttpTaskViewModel : ViewModel
    {
        readonly IHttpTransfer transfer;
        IDisposable taskSub;


        public HttpTaskViewModel(IHttpTransfer transfer)
        {
            this.transfer = transfer;
            //this.Cancel = ReactiveCommand.Create(transfer.Cancel);

            this.taskSub = transfer
                .WhenAnyProperty()
                .Sample(TimeSpan.FromSeconds(1))
                .SubOnMainThread(_ => this.RaisePropertyChanged(String.Empty));
        }


        public string Identifier => this.transfer.Identifier;
        public bool IsUpload => this.transfer.IsUpload;
        public HttpTransferState Status => this.transfer.Status;
        public string Uri => this.transfer.Request.Uri;
        public decimal PercentComplete => this.transfer.PercentComplete;
        public string TransferSpeed => Math.Round(this.transfer.BytesPerSecond.Bytes().Kilobytes, 2) + " Kb/s";
        public string EstimateMinsRemaining => Math.Round(this.transfer.EstimatedCompletionTime.TotalMinutes, 1) + " min(s)";

        //public ICommand Cancel { get; }
        public ICommand MoreInfo { get; }
    }
}
