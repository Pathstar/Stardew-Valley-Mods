
namespace ChatTime;

public class Config
{
    public string timeFormat { get; set; }
    public Config()
    {
        // yyyy-MM-dd HH:mm:ss.fffffff
        timeFormat = "HH:mm:ss";
    }
}