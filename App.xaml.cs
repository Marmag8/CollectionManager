namespace CollectionManager
{
    public partial class App : Application
    {
        public static Utils DataStore { get; } = new();

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new NavigationPage(new CollectionsPage(DataStore)));
        }
    }
}