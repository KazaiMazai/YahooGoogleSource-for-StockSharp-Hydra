using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Ecng.Collections;
using Ecng.Common;
using StockSharp;
using StockSharp.Algo;
using StockSharp.Algo.Candles;
using StockSharp.Algo.History;
using StockSharp.Algo.History.Finam;
using StockSharp.BusinessEntities;
using StockSharp.Hydra;
using StockSharp.Hydra.Core;
using StockSharp.Logging;

namespace Yahoo
{
      class YahooHistorySource : BaseHistorySource
      {
          public static string YahooSecurityIdField= "YahooSecurityId";
          public static string YahooMarketIdField = "YahooMarketId";
          public   static Exchange UsMarket = new Exchange();
          private  List<string> _errorSecurititesList = new List<string>(); 
       
          public YahooHistorySource(ISecurityStorage securityStorage) : base(securityStorage)
          {
              

              
              UsMarket.Name = "Американский фондовый рынок";
              UsMarket.TimeZoneInfo = TimeZoneInfo.Local;     
              UsMarket.IsSupportMarketOrders = true;
              UsMarket.IsSupportAtomicReRegister = false;
            //  UsMarket.WorkingTime.Times = new Range<TimeSpan>[1];
          //    UsMarket.WorkingTime.Times[0] = new Range<TimeSpan>(new TimeSpan(17, 30, 0), new TimeSpan(0, 0, 0));

          }
          
          public IEnumerable<Candle> GetCandles(Security security, DateTime beginDate,DateTime endDate,TimeSpan timeframe)
          {
              if (timeframe != new TimeSpan(1, 0, 0, 0))
                  throw new InvalidDataException("unsupported timeframe " + security.ShortName);

             
              
              {

                  var candleList = new List<TimeFrameCandle>();

                  if (_errorSecurititesList.Any(c => c == security.Id)) return candleList;
                  var web = new WebClient();
                  string data = "";
                  try
                  {
                      data =
                      web.DownloadString(string.Format("http://ichart.finance.yahoo.com/table.csv?s={0}&c={1}",
                                                       security.ShortName, beginDate.Year));
                 
                  }
                  catch
                  {
                      
                  }
                  


                  if (data == "")

                  {
                      _errorSecurititesList.Add(security.Id);
                      return candleList;
                  }

                  data = data.Replace(",", ";");

                  data = data.Replace(".", ",");

                  string[] rows = data.Split('\n');

                  //First row is headers so Ignore it
                  for (int i = 1; i < rows.Length; i++)
                  {
                      if (rows[i].Replace("\n", "").Trim() == "") continue;

                      string[] cols = rows[i].Split(';');

                      var candle = new TimeFrameCandle();

                      string[] parseDate = cols[0].Split("-");


                        candle.OpenTime = new DateTime(int.Parse(parseDate[0]), int.Parse(parseDate[1]),
                                                     int.Parse(parseDate[2]));
                      candle.CloseTime = candle.OpenTime + timeframe;
                      candle.TimeFrame = timeframe;
                      candle.OpenPrice = decimal.Parse(cols[1]);
                      candle.HighPrice = decimal.Parse(cols[2]);
                      candle.LowPrice = decimal.Parse(cols[3]);
                      candle.ClosePrice = decimal.Parse(cols[4]);
                      candle.TotalVolume = decimal.Parse(cols[5]);
                      candle.Security = security;

                      candleList.Add(candle);

                  }
                  var orderedCandleList = candleList.FindAll(
                          c => c.Security.Id == security.Id && c.OpenTime >= beginDate && c.OpenTime <= endDate).OrderBy
                          (c => c.OpenTime);
                  return orderedCandleList;
              }

          }

          public IEnumerable<Security> GetNewSecurities(string symbolFile)
          {
              var fileInfo = new FileInfo(symbolFile);
              if (!fileInfo.Exists) throw new FileNotFoundException("YahooHistorySource: path=" + symbolFile);
              StreamReader contractsFile = File.OpenText(symbolFile);
              var securities = new List<Security>();
               
              while (true)
              {
                  string buf = contractsFile.ReadLine();
                  if (buf == null) break;
                  else
                  {
                      

                      buf = buf.Trim();
                      var secId = SecurityIdGenerator.GenerateId(buf, "Yahoo", UsMarket);
                     var security= SecurityStorage.LoadBy("Id", secId);
                      if(security==null)
                      {

                          security=EntityFactory.Instance.CreateSecurity(SecurityIdGenerator.GenerateId(buf, "Yahoo", UsMarket));
                        
                          security.Id = SecurityIdGenerator.GenerateId(buf, "Yahoo", UsMarket);
                          security.Type = SecurityTypes.Equity;
                          security.Currency = CurrencyTypes.USD;
                          security.Name = buf;
                          security.Exchange = UsMarket;
                          security.MinStepPrice = 0.01m;
                          security.MinStepSize = 0.01m;
                          security.ShortName = buf;
                          security.Class = "Yahoo";
                          security.Code = buf;
                          security.ExtensionInfo = new Dictionary<object, object>();
                          security.ExtensionInfo.Add(YahooSecurityIdField, buf);
                          security.ExtensionInfo.Add(YahooMarketIdField, UsMarket.Name);
                          security.ExtensionInfo.Add("LastCandleTime",(new DateTime(1900,1,1)).To<long>());
                          
                          SecurityStorage.Save(security);


                      }


                      securities.Add(security);

                  }



              }
              contractsFile.Close();
              return securities;



          }
        
      }

 

    

    
}
