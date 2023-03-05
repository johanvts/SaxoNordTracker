module TransactionReader

//#r "nuget: ExcelProvider"
//#r "nuget: FSharp.Data"
open FSharp.Interop.Excel
open FSharp.Data

type SaxoSheet = ExcelFile<"c:\Users\Johan\portfolioreader\Templates\Saxo_Transactions_template.xlsx", ForceString=true>
type NordnetCsv = CsvProvider< @"c:\Users\Johan\portfolioreader\templates\Nordnet_Transactions_template.csv">
type TransactionType = Transfer | Trade | InternalTransfer
type Transaction = {date: System.DateTime; transactionType: TransactionType; instrument: string; instrumentQuery:string; amount: double; instrumentPrice: double; convertedPrice: double; accountedAmount: double}

type System.String with
    member s.ToTransactionType() =
        match s with
            | "KØBT" -> Trade
            | "SOLGT" -> Trade
            | "Handel" -> Trade
            | "Kontantbeløb" -> InternalTransfer
            | "Kontantoverførsel" -> Transfer
            | "INDBETALING" -> Transfer
            | "UDBETALING" -> Transfer
            | "Corporate action" -> InternalTransfer
            | _ -> InternalTransfer
            
    member s.ToFloat() = match System.Double.TryParse s with | true,n -> n | _ -> 0
    member s.ToFloatDanish() = System.Double.Parse(s,System.Globalization.CultureInfo.CreateSpecificCulture("da-DK"))
    member s.ToAmountAndRate() =
        let parts = s.Split('@') |> Array.map(fun part -> Seq.filter(System.Char.IsLetter >> not) part |> System.String.Concat)
        (System.Double.Parse(parts[0]),System.Double.Parse(parts[1]))

let readSaxo (sheet:SaxoSheet) =
    sheet.Data |> Seq.take ((Seq.length sheet.Data) - 1)
    |> Seq.map(fun row ->
                   match row.Type.ToTransactionType() with
                   | InternalTransfer
                   | Transfer ->
                       {date = System.DateTime.Parse(row.Handelsdato
                                                        .Replace("maj","may")
                                                        .Replace("okt","oct"));
                        transactionType = row.Type.ToTransactionType();
                        instrument = row.Instrument;
                        instrumentQuery = row.Instrument;
                        amount = row.``Bogført beløb``.ToFloat();
                        instrumentPrice = 1.0;
                        convertedPrice = 1.0;
                        accountedAmount = row.``Bogført beløb``.ToFloat()}
                   | Trade ->
                       let (amount,rate) = row.Arrangement.ToAmountAndRate()
                       {date = System.DateTime.Parse(row.Handelsdato
                                                        .Replace("maj","may")
                                                        .Replace("okt","oct"));
                        transactionType = Trade
                        instrument = row.Instrument;
                        instrumentQuery = row.Instrument.Substring(0,30);
                        amount = if row.``Bogført beløb``.ToFloat() < 0.0 then amount else -amount;
                        instrumentPrice = rate;
                        convertedPrice = System.Math.Abs(row.``Bogført beløb``.ToFloat() / amount);
                        accountedAmount = row.``Bogført beløb``.ToFloat();})
                
let readNordnet (data:NordnetCsv) =
    data.Rows |> Seq.map(fun row ->
                         match row.Transaktionstype.ToTransactionType() with
                         | InternalTransfer
                         | Transfer ->
                             {date = row.Bogføringsdag;
                              transactionType = row.Transaktionstype.ToTransactionType();
                              instrument=row.Værdipapirer;
                              instrumentQuery=row.ISIN;
                              amount=row.Beløb.ToFloatDanish();                                 
                              instrumentPrice= 1.0;
                              convertedPrice= 1.0;
                              accountedAmount=row.Beløb.ToFloatDanish()}
                         | Trade ->
                             {date = row.Bogføringsdag;
                              transactionType = Trade;
                              instrument=row.Værdipapirer;
                              instrumentQuery=row.ISIN;
                              amount=if row.Beløb.ToFloatDanish() < 0.0 then row.Antal else -row.Antal;
                              instrumentPrice= row.Kurs;
                              convertedPrice= if row.Antal = 0 then row.Kurs else System.Math.Abs(row.Beløb.ToFloatDanish()/row.Antal);
                              accountedAmount=row.Beløb.ToFloatDanish()})
