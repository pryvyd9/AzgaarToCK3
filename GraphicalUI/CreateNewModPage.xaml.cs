using Converter;

namespace GraphicalUI;

public partial class CreateNewModPage : ContentPage
{
	public CreateNewModPage()
	{
		InitializeComponent();

        if (!string.IsNullOrEmpty(Settings.Instance.ModName))
        {
            modNameEntry.Placeholder = $"Current mod name: {Settings.Instance.ModName}";
        }
    }

    private async void applyButton_Clicked(object sender, EventArgs e)
    {
        Settings.Instance.ModName = modNameEntry.Text;

        if (ModManager.DoesModExist())
        {
            var message = "Mod with such name already exists.";
            Log.Warning(message);
            var shouldReplace = await DisplayAlert(message, "If you wish to populate exising mod then click 'Populate Mod' instead." +
                "\n\nIf you replace the mod the files will be replaced on population stage.", "Replace", "Cancel");
            if (!shouldReplace)
            {
                return;
            }
        }

        await ModManager.CreateMod();

        await Navigation.PopModalAsync();
    }

    private async void cancelButton_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}