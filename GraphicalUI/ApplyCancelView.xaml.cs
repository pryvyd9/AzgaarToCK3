namespace GraphicalUI;

public partial class ApplyCancelView : ContentPage
{
	public event EventHandler OnBeforeApply;

	public ApplyCancelView()
	{
		InitializeComponent();
	}

    private async void applyButton_Clicked(object sender, EventArgs e)
    {
        OnBeforeApply?.Invoke(this, e);
        await Navigation.PopModalAsync();
    }

    private async void cancelButton_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}