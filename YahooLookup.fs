module YahooLookup
 
open System.Net.Http
open FSharp.Data

type YahooHistoric = CsvProvider< @"c:\Users\Johan\portfolioreader\templates\yahoo.csv">
// Get symbol from ISIN via;
//let https://query1.finance.yahoo.com/v1/finance/search?quotesCount=1&newsCount=0&listsCount=0&quotesQueryId=%27tss_match_phrase_query%27&q=QUERY
//get quote from symbol via: http://fssnip.net/7WA/title/Download-Yahoo-Finance-data

let findSymbol (isin:string) =
    async {
        use client = new HttpClient()
        
        let url = $"https://query1.finance.yahoo.com/v1/finance/search?quotesCount=1&newsCount=0&listsCount=0&quotesQueryId=%%27tss_match_phrase_query%%27&q={isin}"
        
        let! response = client.GetStringAsync(url) |> Async.AwaitTask
        let symbolIndicator = "symbol\":\""
        let symbolStart = response.IndexOf(symbolIndicator) + symbolIndicator.Length
        let symbolEnd = response.IndexOf("\"",symbolStart)
        let symbol = response.Substring(symbolStart,symbolEnd-symbolStart)
        // Manually prefer Amsterdam over London
        return match symbol.[^1..] with | ".L" -> symbol.[..^2]+".AS" | _ -> symbol 
        }

let getHistoricQuotesFrom (from:System.DateTime) (symbol:string) =
    async {
        use client = new HttpClient()
        let startDate = (int)(from.ToUniversalTime() - System.DateTime.UnixEpoch).TotalSeconds;
        let endDate = (int)(System.DateTime.UtcNow - System.DateTime.UnixEpoch.ToUniversalTime()).TotalSeconds;
        let url = $"https://query1.finance.yahoo.com/v7/finance/download/{symbol}?period1={startDate}&period2={endDate}&interval=1d&events=history"
        let! response = client.GetStringAsync(url) |> Async.AwaitTask

        let takeJustFloats (a,s:string) = match System.Double.TryParse(s) with | true,x -> Some (a,x) | _ -> None
        return  YahooHistoric.Parse(response).Rows |> Seq.map(fun row -> row.Date, row.``Adj Close``) |> Seq.choose takeJustFloats
    }
    
let getHistoricQuotes = getHistoricQuotesFrom (System.DateTime.Now.AddYears(-4))
