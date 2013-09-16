﻿using Ecng.Collections;
using StockSharp.Algo.History;
using StockSharp.Hydra.Yahoo;
using StockSharp.Logging;
using Yahoo;

namespace StockSharp.Hydra.YahooGoogle
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using Ecng.Collections;
    using Ecng.Common;
    using Ecng.Xaml;
    using StockSharp.Algo;
    using StockSharp.Algo.Candles;
    using StockSharp.Algo.History.Finam;
 
    using StockSharp.BusinessEntities;
    using StockSharp.Hydra.Core;
    using StockSharp.Algo.Storages;
    class YahooGoogleSource : BaseMarketDataSource, ISecuritySource
    {
        [DisplayName("Настройки источника Yahoo-Google EoD")]
        private sealed class YahooSettings : MarketDataSourceSettings
        {
            public YahooSettings(MarketDataSourceSettings settings)
                : base(settings)
            {
            }

            [Category("Yahoo")]
            [DisplayName("Временной отступ")]
            [Description("Временной отступ в днях от текущей даты, начиная c которого необходимо загружать данные")]
            public int YahooOffset
            {
                get { return ExtensionInfo["YahooOffset"].To<int>(); }
                set { ExtensionInfo["YahooOffset"] = value; }
            }
           
           
            [Category("Yahoo")]
            [DisplayName("Начальная дата")]
            [Description("Начальная дата для перезагрузки всех данных")]
            public DateTime StartFrom
			{
				get { return ExtensionInfo["StartFrom"].To<DateTime>(); }
				set { ExtensionInfo["StartFrom"] = value; }
			}

            [Category("Yahoo")]
            [DisplayName("Режим перезагрузки")]
            [Description("Начальная дата для перезагрузки всех данных")]
            public bool ReDownLoad
            {
                get { return ExtensionInfo["ReDownLoad"].To<bool>(); }
                set { ExtensionInfo["ReDownLoad"] = value; }
            }
		 


        }

        private YahooSettings _settings;
        private YahooGoogelHistorySource _source;
        private IEnumerable<Security> _selectedSecurities;
        private YahooGoogleSecurityStorage _yahooGoogleSecurityStorage;
         

        protected override void ApplySettings(MarketDataSourceSettings settings, bool isNew)
        {
            _settings = new YahooSettings(settings);

            if (isNew)
            {
                _settings.YahooOffset = 1;
                _settings.StartFrom = new DateTime(1900,1,1);
               _settings.ReDownLoad = true;
            }
            
             _yahooGoogleSecurityStorage = new YahooGoogleSecurityStorage(SecurityStorage, EntityRegistry);
              _source = new YahooGoogelHistorySource(_yahooGoogleSecurityStorage) { DumpFolder = GetTempPath() };
           //   if (!EntityRegistry.Exchanges.Any(e => e.Name == YahooGoogelHistorySource.UsMarket.Name)) EntityRegistry.Exchanges.Save(YahooGoogelHistorySource.UsMarket);
        }

        public override void Load()
        {

            if (this.GetAllSecurity() != null) _selectedSecurities = _yahooGoogleSecurityStorage.YahooSecurities;
            else _selectedSecurities = Securities;

            DateTime startDate;
            if (_settings.ReDownLoad) startDate = _settings.StartFrom;
            else startDate = DateTime.Today - TimeSpan.FromDays(_settings.YahooOffset);
            var endDate = DateTime.Today;

            var allDates = new List<DateTime>();
             for (; startDate <= endDate; startDate = startDate.AddDays(1))
            {
                allDates.Add(startDate);
            }
             foreach (var security in _selectedSecurities)
            {
                if (State!= MarketDataSourceStates.Started)
                {
                    this.AddInfoLog("Прерывание загрузки данных.");
                    return;
                }

              
                LoadCandles(security, allDates);
            }
           
        }


        private void LoadCandles(Security security, List<DateTime> allDates)
        {
             var periods = security.GetCandlePeriods();

            if (security.ContainsSource<Candle>(this) && !periods.IsEmpty())
            {
                foreach (var period in periods)
                {
                    if (State != MarketDataSourceStates.Started)
                    {
                        this.AddInfoLog("Прерывание загрузки данных.");
                        return;
                    }

                    var storage = StorageRegistry.GetCandleStorage(new CandleSeries(typeof(TimeFrameCandle), security, period), GetDrive());
                    var emptyDates = allDates.Except(storage.Dates).ToArray();

                    if (emptyDates.IsEmpty())
                    {
                        this.AddInfoLog("Нет несохраненных дней для загрузки {0} свечек по {1}.", period, security.Id);
                        continue;
                    }

                    foreach (var emptyDate in emptyDates)
                    {
                        if (State != MarketDataSourceStates.Started)
                        {
                            this.AddInfoLog("Прерывание загрузки данных.");
                            return;
                        }

                        this.AddInfoLog("Старт загрузки {0} свечек за {1} для {2}.", period, emptyDate.ToShortDateString(), security.Id);

                        var candles = _source.GetCandles(security, emptyDate, emptyDate, period);
                        if (candles.Count() > 0)
                        {
                            SaveCandles(security, period, candles);
                        }
                    }
                }
            }
        }



        public override Uri Icon
        {
            get { return "yahoo.png".GetResourceUrl(GetType()); }
        }

        public override MarketDataSourceSettings Settings
        {
            get { return _settings; }
        }

        public string Name
        {
            get { return "Yahoo+Google"; }
        }

        public override string Description
        {
            get { return "Источник предназначен для получения EoD данных с Yahoo и Google."; }
        }

        public override Type[] SupportedDataTypes
        {
            get { return new[] { typeof(Candle) }; }
        }

       
        public override bool IsSupportLookupSecurities
        {
            get { return false; }
        }

        public override bool IsSupportHistoricalData
        {
            get { return true; }
        }


        public override void SaveSettings()
        {
            base.SaveSettings();

            if (_source != null)
                _source.DumpFolder = GetTempPath();
        }

        

        private string GetTempPath()
        {
            var tempPath = Path.Combine(_settings.StorageFolder, "TemporaryFiles");

            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);

            return tempPath;
        }
      
       
        public IEnumerable<Security> GetNewSecurities( )
        {

            this.AddDebugLog("GetNewSecurities()");
            EntityRegistry.ExchangeBoards.Save(_source.GetSmartExchangeBoard());

            var securities = new List<Security>();

           
            securities.AddRange(  _source.GetSecuritiesFromTxt());
             

            var timeframe = TimeSpan.FromDays(1);
            foreach (var security in securities)
            {
               
               
                security.AddSource(typeof (Candle), this);
                EntityRegistry.Securities.Save(security);
                
            }

            return securities;



        }

        public IEnumerable<Security> GetLookupSecurities(Security criteria)
        {
            return new[] {criteria};
        }
   
    }

    
}
