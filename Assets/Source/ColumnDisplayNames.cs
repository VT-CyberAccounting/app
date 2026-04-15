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
        { "Common shares outstanding last year",     "Common shares outstanding last year" }
    };

    public static string GetDisplayName(string csvHeader)
    {
        return DisplayNames.TryGetValue(csvHeader, out string name) ? name : csvHeader;
    }
}