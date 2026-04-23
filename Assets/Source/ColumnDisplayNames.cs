using System.Collections.Generic;

public static class ColumnDisplayNames
{
    private static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        { "Environmental",                           "Environmental" },
        { "Social",                                  "Social" },
        { "Governance",                              "Governance" },
        { "ESG_score",                               "ESG Score" },
        { "Current Assets",                          "Current Assets" },
        { "Assets",                                  "Total Assets" },
        { "Cash",                                    "Cash and Cash Equivalents" },
        { "Inventory",                               "Inventory" },
        { "Current Marketable Securities",           "Current Marketable Securities" },
        { "Current Liabilities",                     "Current Liabilities" },
        { "Liabilities",                             "Total Liabilities" },
        { "Property, Plant and Equipment",           "Property, Plant and Equipment" },
        { "Preferred/Preference Stock",              "Preferred/Preference Stock" },
        { "Allowance for Doubtful Receivables",      "Allowance for Doubtful Accounts" },
        { "Total Receivables",                       "Total Receivables" },
        { "Stockholders Equity",                     "Stockholders Equity" },
        { "Cost of Goods Sold",                      "Cost of Goods Sold" },
        { "Dividends - Preferred/Preference",        "Preferred/Preference Dividends" },
        { "Dividends",                               "Dividends Paid" },
        { "Earnings Before Interest and Taxes",      "Earnings Before Interest and Taxes" },
        { "Earnings Per Share (Basic)",              "Earnings Per Share (Basic)" },
        { "Net Income (Loss)",                       "Net Income (Loss)" },
        { "Net Income Adjusted for common stocks",   "Net Income Available to Common Shareholders" },
        { "Sales/Turnover (Net)",                    "Sales/Turnover (Net)" },
        { "Interest and Related Expense",            "Interest Expense" },
        { "Common Shares Outstanding",               "Common Shares Outstanding" },
        { "Total Debt Including Current",            "Total Debt (Including Current Portion)" },
        { "Price Close - Annual -",                  "Annual Close Price" },
        { "Net receivables",                         "Net Receivables" },
        { "Total assets last year",                  "Total Assets Last Year" },
        { "Net receivables last year",               "Net Receivables Last Year" },
        { "Inventory last year",                     "Inventory Last Year" },
        { "Stockholder equity last year",            "Stockholder Equity Last Year" },
        { "Cost of Goods Sold last year",            "Cost of Goods Sold Last Year" },
        { "Common shares outstanding last year",     "Common Shares Outstanding Last Year" },

        // Case study columns (answer / solution / error surfaces)
        { "Working capital",                         "Working Capital" },
        { "Current ratio",                           "Current Ratio" },
        { "Quick ratio",                             "Quick Ratio (Acid-Test)" },
        { "Accounts receivable turnover",            "Accounts Receivable Turnover" },
        { "Average days to collect receivables",     "Days Sales Outstanding (DSO)" },
        { "Inventory turnover",                      "Inventory Turnover" },
        { "Average days to collect inventory",       "Days Inventory Outstanding (DIO)" },
        { "Debt to assets",                          "Debt-to-Assets Ratio" },
        { "Debt to equity",                          "Debt-to-Equity Ratio" },
        { "Number of times interest is earned",      "Times Interest Earned (TIE)" },
        { "Net margin (or return on sales)",         "Net Profit Margin" },
        { "Asset turnover ratio",                    "Asset Turnover Ratio" },
        { "Return on investment",                    "Return on Investment (ROI)" },
        { "Return on equity",                        "Return on Equity (ROE)" },
        { "Earnings per share",                      "Earnings Per Share (EPS)" },
        { "Book value per share",                    "Book Value Per Share" },
        { "Price-Earnings (P/E) ratio",              "Price-to-Earnings (P/E) Ratio" },
        { "Dividend yield",                          "Dividend Yield" }
    };

    private static readonly HashSet<string> NonCurrencyColumns = new HashSet<string>
    {
        "Environmental",
        "Social",
        "Governance",
        "ESG_score",
        "Common Shares Outstanding",
        "Common shares outstanding last year",

        // Case study: ratios, percentages, day-counts — not dollar amounts
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
        "Price-Earnings (P/E) ratio",
        "Dividend yield"
    };

    public static string GetDisplayName(string csvHeader)
    {
        return DisplayNames.TryGetValue(csvHeader, out string name) ? name : csvHeader;
    }

    public static bool IsCurrencyColumn(string csvHeader)
    {
        return !string.IsNullOrEmpty(csvHeader) && !NonCurrencyColumns.Contains(csvHeader);
    }
}