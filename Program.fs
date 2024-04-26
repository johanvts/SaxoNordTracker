open TransactionReader
open YahooLookup
open PriceUpdate
open Accumulation
open Plotly.NET

//let saxo = new SaxoSheet("Data\Saxo_Transactions_Example.xlsx")
let saxoA = new SaxoSheet(@"C:\Users\Johan\portfolioreader\data\Transactions_9220037_2019-02-06_2024-02-12.xlsx")
let saxoB = new SaxoSheet(@"c:\Users\Johan\portfolioreader\data\Transactions_9345630_2019-04-08_2024-03-12.xlsx")
//let nordnet = NordnetCsv.Load(__SOURCE_DIRECTORY__ + "\Data\Nordnet_Transactions_Example.csv")
let nordnet = NordnetCsv.Load(@"c:\Users\Johan\portfolioreader\data\transactions-and-notes-export.csv")

let transactions = readNordnet nordnet |> Seq.append (readSaxo saxoA) |> Seq.append (readSaxo saxoB) |> Seq.sortBy(fun row -> row.date)
let symbolsByQuery = transactions |> Seq.map(fun transaction -> transaction.instrumentQuery) |> Set.ofSeq |> Set.toList |> List.where(fun query -> not (System.String.IsNullOrEmpty query) && query.Length > 10) |>  List.map(fun query -> (query, (findSymbol query |> Async.RunSynchronously))) |> Map.ofList


let symbols = symbolsByQuery.Values |> Seq.distinct |> Seq.toList
let currencyBySymbol = symbolsByQuery.Keys |> Seq.map(fun query ->
                                                       (symbolsByQuery[query],
                                                        (match transactions |> Seq.tryFind(fun transaction -> transaction.instrumentQuery = query) with
                                                         | Some transaction -> transaction.currency
                                                         | None -> "DKK"))) |> dict

let transactionUpdates = transactions |> Seq.map TransactionUpdate
let priceUpdates = (generatePriceCorrectionTransactions symbols currencyBySymbol) |> Seq.map PriceUpdate
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
