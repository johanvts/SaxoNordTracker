module Accumulation

open TransactionReader
open PriceUpdate

// The idea here is to accumulate updates into two maps keyed on Symbol
// The first map tracks the number of shares held of the symbol
// The second maps trackts the price of the symbol

let addPriceupdate (_,amounts:Map<string,float>,prices:Map<string,float>) (priceUpdate:PriceUpdate) =
    (priceUpdate.date,
     amounts,
     prices.Add(priceUpdate.symbol,priceUpdate.price))

let addTransaction (symbolsByQuery:Map<string,string>) (_,amounts:Map<string,float>,prices:Map<string,float>) (transaction:Transaction) =
    let cashSymbol = symbolsByQuery.["Cash"];
    let transferSymbol = symbolsByQuery.["Transfer"];
    let ok, value = symbolsByQuery.TryGetValue(transaction.instrumentQuery)
    let symbol,amounts =
        match transaction.transactionType with
        | Trade when ok -> value, amounts.Add(value,match amounts.TryFind(value) with | Some number -> number + transaction.amount | None -> transaction.amount).Add(cashSymbol, amounts.[cashSymbol] - transaction.amount * transaction.convertedPrice)
        | Trade -> failwith $"Trade in unknow symbol {transaction.instrumentQuery}"
        | InternalTransfer -> cashSymbol, amounts.Add(cashSymbol, amounts.[cashSymbol] + transaction.amount)
        | Transfer -> transferSymbol, amounts.Add(transferSymbol, amounts.[transferSymbol] + transaction.amount).Add(cashSymbol, amounts.[cashSymbol] + transaction.amount)
    (transaction.date,
     amounts,
     prices.Add(symbol,transaction.convertedPrice))

let addUpdate (symbolsByQuery:Map<string,string>) state = function
    | PriceUpdate pu -> addPriceupdate state pu
    | TransactionUpdate tu -> addTransaction symbolsByQuery state tu


let accumulateUpdatesBySymbol (symbolsByQuery:Map<string,string>) updates =
    let cashSymbol = "Cash"
    let transferSymbol = "Transfer"
    let startState = (System.DateTime.Now,Map.empty.Add(cashSymbol,0.0).Add(transferSymbol,0.0),Map.empty)
    updates |> Seq.scan (addUpdate (symbolsByQuery.Add("Cash", cashSymbol).Add("Transfer", transferSymbol))) startState |> Seq.skip 1

