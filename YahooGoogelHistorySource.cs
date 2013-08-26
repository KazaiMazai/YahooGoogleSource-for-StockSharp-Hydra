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
      class YahooGoogelHistorySource : BaseHistorySource
      {
          public static string YahooSecurityIdField= "YahooSecurityId";
          public static string YahooMarketIdField = "YahooMarketId";
          private  List<string> _errorSecurititesList = new List<string>();

          private DateTime _cachedBeginDate ;
          private DateTime _cachedEndDate;
          private Security _cachedSecurity;
          private TimeSpan _cachedTimeframe;
          private List<TimeFrameCandle> _cachedCandleList;
          private DateTime _lastUpdate;

          public YahooGoogelHistorySource(ISecurityStorage securityStorage) : base(securityStorage)
          {
              var securities = new List<Security>();
              string filepath = Directory.GetCurrentDirectory() + "\\YahooGoogleSourceTickers.txt";

              var fileInfo = new FileInfo(filepath);
              if (!fileInfo.Exists)
              {
                  File.CreateText(filepath);
                  
              }

          }
          
          public IEnumerable<Candle> GetCandles(Security security, DateTime beginDate,DateTime endDate,TimeSpan timeframe)
          {
              if (Math.Abs(timeframe.TotalHours - 24) > 0.000001)
                  throw new InvalidDataException("unsupported timeframe " + security.Code);

              
              {
                  if(_cachedCandleList!=null)
                      if(_cachedCandleList.Count>0)
                      {
                          if(_cachedSecurity.Code== security.Code && Math.Abs(_cachedTimeframe.TotalSeconds - timeframe.TotalSeconds) < 0.00001)
                              if(DateTime.Now -_lastUpdate<TimeSpan.FromMinutes(30))
                              return _cachedCandleList.Where(c => c.OpenTime >= beginDate && c.OpenTime <= endDate);
                          }

                  var candleList = new List<TimeFrameCandle>();

                  if (_errorSecurititesList.Any(c => c == security.Id)) return candleList;
                  var web = new WebClient();
                  string data = "";
                  try
                  {
                      data =
                      web.DownloadString(string.Format("http://ichart.finance.yahoo.com/table.csv?s={0}&c={1}",
                                                       security.Code, 1900));
                 
                  }
                  catch
                  {
                      
                  }

                  string[] rows;
                  if (data == "")
                  {

                      try
                      {
                          string url = string.Format("http://www.google.com/finance/historical?output=csv&q={0}",
                                                security.Code);

                          HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                          WebResponse response = request.GetResponse();
                          StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.ASCII);
                          data = reader.ReadToEnd();
                      }
                      catch
                      {

                      }

                      if (data == "")
                         {
                              _errorSecurititesList.Add(security.Id);
                              using (StreamWriter w = File.AppendText(Directory.GetCurrentDirectory() + "\\ErrorTickers.txt"))
                              {
                                 w.WriteLine(security.Id);
                              }
                              return candleList;
                          }
                      else
                      {
                           data = data.Replace(",", ";");

                          data = data.Replace(".", ",");

                          rows = data.Split('\n');

                          //First row is headers so Ignore it
                          for (int i = 1; i < rows.Length; i++)
                          {
                              if (rows[i].Replace("\n", "").Trim() == "") continue;

                              string[] cols = rows[i].Split(';');



                              try
                              {
                                   
                              var OpenTime = DateTime.Parse(cols[0]);


                              decimal OpenPrice = -6m, HighPrice = -6m, LowPrice = -6m, ClosePrice = -6m, TotalVolume = -6m;
                              
                              if (!decimal.TryParse(cols[1], out OpenPrice))
                                      throw new InvalidDataException("Google data error. Symbol: " + security.Id);
                                  if (!decimal.TryParse(cols[2], out HighPrice))
                                      throw new InvalidDataException("Google data error. Symbol: " + security.Id);
                                  if (!decimal.TryParse(cols[3], out LowPrice))
                                      throw new InvalidDataException("Google data error. Symbol: " + security.Id);
                                  if (!decimal.TryParse(cols[4], out ClosePrice))
                                      throw new InvalidDataException("Google data error. Symbol: " + security.Id);
                                  if (!decimal.TryParse(cols[5], out TotalVolume))
                                      throw new InvalidDataException("Google data error. Symbol: " + security.Id);
                             

                              var candle = new TimeFrameCandle();


                              candle.OpenPrice = OpenPrice;

                              candle.HighPrice = HighPrice;

                              candle.LowPrice = LowPrice;

                              candle.ClosePrice = ClosePrice;

                              candle.TimeFrame = timeframe;
                              candle.OpenTime = OpenTime;
                              candle.CloseTime = OpenTime + timeframe;

                              candle.TotalVolume = TotalVolume;

                              candle.Security = security;

                              candleList.Add(candle);
                              }
                              catch (Exception ex)
                              {

                                  _errorSecurititesList.Add(security.Id);
                                  using (StreamWriter w = File.AppendText(Directory.GetCurrentDirectory() + "\\ErrorTickers.txt"))
                                  {
                                      w.WriteLine(security.Id);
                                  }
                              }

                          }
                      }


                  }

                 

                  data = data.Replace(",", ";");

                  data = data.Replace(".", ",");

                  rows = data.Split('\n');

                  //First row is headers so Ignore it
                  for (int i = 1; i < rows.Length; i++)
                  {
                      if (rows[i].Replace("\n", "").Trim() == "") continue;

                      string[] cols = rows[i].Split(';');

                      var candle = new TimeFrameCandle();

                      string[] parseDate = cols[0].Split("-");

                      try
                      { 
                      
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
                      catch (Exception ex)
                      {
                          _errorSecurititesList.Add(security.Id);
                          using (StreamWriter w = File.AppendText(Directory.GetCurrentDirectory() + "\\ErrorTickers.txt"))
                          {
                              w.WriteLine(security.Id);
                          }
                      }

                  }
                   _cachedCandleList  = candleList.OrderBy
                          (c => c.OpenTime).ToList();
                  _cachedBeginDate = _cachedCandleList.First().OpenTime;
                   _cachedEndDate = _cachedCandleList.Last().OpenTime;
                  _cachedSecurity = security;
                  _cachedTimeframe = timeframe;
                  _lastUpdate = DateTime.Now;
                   return _cachedCandleList.Where(c => c.OpenTime >= beginDate && c.OpenTime <= endDate); 
              }

          }

          public ExchangeBoard GetSmartExchangeBoard()
          {

              var exchangeBoard = ExchangeBoard.GetOrCreateBoard("SMART", notExistingExchangeBoardCode => new ExchangeBoard
              {
                  Exchange = new Exchange {Name = notExistingExchangeBoardCode + " Exchange", EngName = notExistingExchangeBoardCode + " Exchange", RusName = notExistingExchangeBoardCode + " по-русски" },
                  Code = notExistingExchangeBoardCode,
                  IsSupportAtomicReRegister = true,
                  IsSupportMarketOrders = true,
                  WorkingTime = ExchangeBoard.Nasdaq.WorkingTime
              });


              return exchangeBoard;

          }

          public List<Security> GetSecuritiesFromTxt()
          {
              var securities = new List<Security>();
              string filepath = Directory.GetCurrentDirectory() + "\\YahooGoogleSourceTickers.txt";

              var fileInfo = new FileInfo(filepath);
              if (!fileInfo.Exists)
              {
                  File.CreateText(filepath);
                  return securities;
              }


              StreamReader contractsFile = File.OpenText(filepath);
           
              while (true)
              {
                  string line=   contractsFile.ReadLine();
                  if (line == null) break;
                  if(line.Length==0) break;

                  line = line.Trim();
                  string[] tickers = line.Split(' ');
                  foreach (var ticker in tickers)
                  {
                      var securityId = ticker.Trim();
                      var splitedId = securityId.Split('@');

                      var code = splitedId[0];
                      var exchangeBoardCode = splitedId[1];

                      var security = SecurityStorage.LoadBy("Id", securityId);
                      if(security==null)
                      {
                          var exchangeBoard = ExchangeBoard.GetOrCreateBoard(exchangeBoardCode, notExistingExchangeBoardCode => new ExchangeBoard
                          {
                              Exchange = new Exchange { Name = notExistingExchangeBoardCode + " Exchange", EngName = notExistingExchangeBoardCode + " Exchange", RusName = notExistingExchangeBoardCode + " по-русски"},
                              Code = notExistingExchangeBoardCode,
                              IsSupportAtomicReRegister = true,
                              IsSupportMarketOrders = true,
                              WorkingTime = ExchangeBoard.Nasdaq.WorkingTime
                          });


                          security = EntityFactory.Instance.CreateSecurity(securityId);

                          security.Id = securityId;
                          security.Type = SecurityTypes.Stock;
                          security.Currency = CurrencyTypes.USD;
                          security.Name = code;
                          security.ExchangeBoard = exchangeBoard;
                          security.MinStepPrice = 0.01m;
                          security.MinStepSize = 0.01m;
                          security.ShortName = code;
                          security.Code = code;

                          security.ExtensionInfo = new Dictionary<object, object>();
                          security.ExtensionInfo.Add(YahooSecurityIdField, code);
                          security.ExtensionInfo.Add(YahooMarketIdField, security.ExchangeBoard.Code);
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
