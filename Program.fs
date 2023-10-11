open TransactionReader
open YahooLookup
open PriceUpdate
open Accumulation
open System.Collections.Generic
open Plotly.NET

let saxo = new SaxoSheet("Data\Saxo_Transactions_Example.xlsx")
let nordnet = NordnetCsv.Load(__SOURCE_DIRECTORY__ + "\Data\Nordnet_Transactions_Example.csv")

let transactions = readNordnet nordnet |> Seq.append (readSaxo saxo) |> Seq.sortBy(fun row -> row.date)
let symbolsByQuery = transactions |> Seq.map(fun transaction -> transaction.instrumentQuery) |> Set.ofSeq |> Set.toList |> List.where(fun query -> not (System.String.IsNullOrEmpty query) && query.Length > 10) |>  List.map(fun query -> (query, (findSymbol query |> Async.RunSynchronously))) |> Map.ofList
let symbols = symbolsByQuery.Values |> Seq.distinct |> Seq.toList
let currencyBySymbol = symbolsByQuery.Values |>  Seq.map(fun symbol -> (symbol, (findCurrency symbol |> Async.RunSynchronously))) |> dict
let transactionUpdates = transactions |> Seq.map (fun t -> TransactionUpdate t)
let priceUpdates = (generatePriceCorrectionTransactions symbols currencyBySymbol) |> Seq.map(fun pu -> PriceUpdate pu)
let updates = Seq.append transactionUpdates priceUpdates |> Seq.sortBy(fun update -> update.date)


let aggregatesBySymbol = updates |> accumulateUpdatesBySymbol symbolsByQuery

let aggregatedByMap (symbolsByGroup:Map<string,string list>) group =
    let symbols = symbolsByGroup.[group]
    aggregatesBySymbol |> Seq.map(fun (date,amounts:Map<string,double>,prices:Map<string,double>) -> (date,symbols |> List.where(fun symbol -> amounts.ContainsKey symbol && prices.ContainsKey symbol) |> List.sumBy(fun symbol -> amounts.[symbol] * prices.[symbol])))
    
let symbolGroups = [
    ("Stocks",symbols);
    ("Cash",["Cash"]);
    ("Transfer",["Transfer"]);
    ("Value", "Cash"::symbols)] |> Map.ofList

[
Chart.Area(aggregatedByMap symbolGroups "Value",Name="Value")
Chart.Area(aggregatedByMap symbolGroups "Transfer",Name="Transfer",LineWidth=0)
Chart.Line(aggregatedByMap symbolGroups "Stocks",Name="Stocks")
Chart.Line(aggregatedByMap symbolGroups "Cash",Name="Cash")
]
|> Chart.combine
|> Chart.withSize(1200.,800.)
|> Chart.withTitle("Portfolio development")
|> Chart.withXAxis(LayoutObjects.LinearAxis.init(Title = Title.init"Time"))
|> Chart.withYAxis(LayoutObjects.LinearAxis.init(Title = Title.init"DKK"))
|> Chart.saveHtml("Portfolio")
