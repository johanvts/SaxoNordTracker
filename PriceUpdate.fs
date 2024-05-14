module PriceUpdate

open System.Collections.Generic
open TransactionReader
open YahooLookup

type PriceUpdate = {date: System.DateTime; symbol:string; price: double}


type Update =
    | TransactionUpdate of Transaction
    | PriceUpdate of PriceUpdate
        with member x.date = match x with | TransactionUpdate tu -> tu.date | PriceUpdate pu -> pu.date

let generatePriceCorrectionTransactions (symbols: string list) (currencyBySymbol:IDictionary<string,string>) =
    let yahooQuotes = Seq.zip symbols (symbols |> List.map getHistoricQuotes |> Async.Parallel |> Async.RunSynchronously)
    yahooQuotes |> Seq.collect(fun (symbol, quotes) -> quotes |> Seq.map(fun (date,quote) -> {date = date; symbol = symbol;  price= quote * (match currencyBySymbol.[symbol] with | "USD" -> 6.0 | "EUR" -> 7.44 | _ -> 1.0)}))
