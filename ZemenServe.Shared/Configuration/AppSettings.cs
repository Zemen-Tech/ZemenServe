namespace ZemenServe.Shared.Configuration;

public class AppSettings
{
    public string ServerHost { get; set; } = "127.0.0.1";
    public int ServerPort { get; set; } = 5000;
    public string DatabasePath { get; set; } = "zemenserve.db";
    public string CurrencySymbol { get; set; } = "ETB";

    public string ServerUrl => $"http://{ServerHost}:{ServerPort}/orderhub";
}
