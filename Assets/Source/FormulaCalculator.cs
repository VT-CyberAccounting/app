using System.Collections.Generic;

public static class FormulaCalculator
{
    public static readonly string[] FormulaColumns = {
        "Working capital",
        "Current ratio",
        "Quick ratio",
        "Accounts receivable turnover",
        "Average days to collect receivables",
        "Inventory turnover",
        "Average days to collect inventory",
        "Debt to assets",
        "Debt to equity",
        "Number of times interest is earned",
        "Net margin (or return on sales)",
        "Asset turnover ratio",
        "Return on investment",
        "Return on equity",
        "Earnings per share",
        "Book value per share",
        "Price-Earnings (P/E) ratio",
        "Dividend yield"
    };

    public struct Inputs
    {
        public float CurrentAssets;
        public float CurrentLiabilities;
        public float Cash;
        public float CurrentMarketableSecurities;
        public float Inventory;
        public float InventoryLastYear;
        public float NetReceivables;
        public float NetReceivablesLastYear;
        public float Sales;
        public float COGS;
        public float Assets;
        public float AssetsLastYear;
        public float Liabilities;
        public float StockholdersEquity;
        public float StockholdersEquityLastYear;
        public float EBIT;
        public float InterestExpense;
        public float NetIncome;
        public float NetIncomeCommon;
        public float CommonShares;
        public float PreferredStock;
        public float PreferredDividends;
        public float Dividends;
        public float Price;
    }

    public static Inputs ExtractInputs(float[] numericValues, Dictionary<string, int> colIndex)
    {
        return new Inputs
        {
            CurrentAssets = Get(numericValues, colIndex, "Current Assets"),
            CurrentLiabilities = Get(numericValues, colIndex, "Current Liabilities"),
            Cash = Get(numericValues, colIndex, "Cash"),
            CurrentMarketableSecurities = Get(numericValues, colIndex, "Current Marketable Securities"),
            Inventory = Get(numericValues, colIndex, "Inventory"),
            InventoryLastYear = Get(numericValues, colIndex, "Inventory last year"),
            NetReceivables = Get(numericValues, colIndex, "Net receivables"),
            NetReceivablesLastYear = Get(numericValues, colIndex, "Net receivables last year"),
            Sales = Get(numericValues, colIndex, "Sales/Turnover (Net)"),
            COGS = Get(numericValues, colIndex, "Cost of Goods Sold"),
            Assets = Get(numericValues, colIndex, "Assets"),
            AssetsLastYear = Get(numericValues, colIndex, "Total assets last year"),
            Liabilities = Get(numericValues, colIndex, "Liabilities"),
            StockholdersEquity = Get(numericValues, colIndex, "Stockholders Equity"),
            StockholdersEquityLastYear = Get(numericValues, colIndex, "Stockholder equity last year"),
            EBIT = Get(numericValues, colIndex, "Earnings Before Interest and Taxes"),
            InterestExpense = Get(numericValues, colIndex, "Interest and Related Expense"),
            NetIncome = Get(numericValues, colIndex, "Net Income (Loss)"),
            NetIncomeCommon = Get(numericValues, colIndex, "Net Income Adjusted for common stocks"),
            CommonShares = Get(numericValues, colIndex, "Common Shares Outstanding"),
            PreferredStock = Get(numericValues, colIndex, "Preferred/Preference Stock"),
            PreferredDividends = Get(numericValues, colIndex, "Dividends - Preferred/Preference"),
            Dividends = Get(numericValues, colIndex, "Dividends"),
            Price = Get(numericValues, colIndex, "Price Close - Annual -")
        };
    }

    public static float[] Compute(in Inputs x)
    {
        float[] r = new float[FormulaColumns.Length];

        r[0] = x.CurrentAssets - x.CurrentLiabilities;
        r[1] = Div(x.CurrentAssets, x.CurrentLiabilities);
        r[2] = Div(x.Cash + x.CurrentMarketableSecurities + x.NetReceivables, x.CurrentLiabilities);

        float avgRecv = 0.5f * (x.NetReceivables + x.NetReceivablesLastYear);
        r[3] = Div(x.Sales, avgRecv);
        r[4] = Div(365f, r[3]);

        float avgInv = 0.5f * (x.Inventory + x.InventoryLastYear);
        r[5] = Div(x.COGS, avgInv);
        r[6] = Div(365f, r[5]);

        r[7] = Div(x.Liabilities, x.Assets);
        r[8] = Div(x.Liabilities, x.StockholdersEquity);
        r[9] = Div(x.EBIT, x.InterestExpense);
        r[10] = Div(x.NetIncome, x.Sales);

        float avgAssets = 0.5f * (x.Assets + x.AssetsLastYear);
        r[11] = Div(x.Sales, avgAssets);
        r[12] = Div(x.NetIncome, avgAssets);

        float avgEquity = 0.5f * (x.StockholdersEquity + x.StockholdersEquityLastYear);
        r[13] = Div(x.NetIncome, avgEquity);

        r[14] = Div(x.NetIncome - x.PreferredDividends, x.CommonShares);
        r[15] = Div(x.StockholdersEquity - x.PreferredStock, x.CommonShares);
        r[16] = Div(x.Price, r[14]);
        r[17] = Div(x.Dividends, x.CommonShares * x.Price);

        return r;
    }

    private static float Get(float[] values, Dictionary<string, int> idx, string name)
    {
        if (values == null) return 0f;
        if (!idx.TryGetValue(name, out int i)) return 0f;
        if (i < 0 || i >= values.Length) return 0f;
        return values[i];
    }

    private static float Div(float a, float b)
    {
        return b == 0f ? 0f : a / b;
    }
}
