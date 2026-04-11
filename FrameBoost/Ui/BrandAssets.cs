using System.Reflection;

namespace FrameBoost.Ui;

internal static class BrandAssets
{
    private const string DashboardWatermarkResourceName = "CloudFrame.Assets.Branding.ChibiCloudWatermark.png";
    private static readonly Lazy<Image?> DashboardWatermark = new(LoadDashboardWatermark);

    public static Image? GetDashboardWatermark() => DashboardWatermark.Value;

    private static Image? LoadDashboardWatermark()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(DashboardWatermarkResourceName);
            if (stream is null)
            {
                return null;
            }

            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch
        {
            return null;
        }
    }
}
