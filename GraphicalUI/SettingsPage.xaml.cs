using Converter;

namespace GraphicalUI;

public partial class SettingsPage : ContentPage
{
	public SettingsPage()
	{
		InitializeComponent();

        settingsEditor.Text = SettingsManager.ToJson();
    }

    private async void applyButton_Clicked(object sender, EventArgs e)
    {
        if (SettingsManager.TryLoadFromString(settingsEditor.Text))
        {
            await Navigation.PopModalAsync();
            return;
        }

        var message = "Failed to save settings";
        await DisplayAlert(message, "Settings were not in correct format.", "Ok");
        Log.Warning(message);
    }

    private async void cancelButton_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}