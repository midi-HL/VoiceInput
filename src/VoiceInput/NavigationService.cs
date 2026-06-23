using System;

namespace VoiceInput
{
    public static class NavigationService
    {
        public static event Action<string>? NavigationRequested;

        public static void NavigateTo(string pageTag)
        {
            NavigationRequested?.Invoke(pageTag);
        }

        public static void NavigateToHome()
        {
            NavigateTo("Home");
        }

        public static void NavigateToSettings()
        {
            NavigateTo("Settings");
        }

        public static void NavigateToLyrics()
        {
            NavigateTo("Lyrics");
        }
    }
}
