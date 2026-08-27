namespace WeatherClockWidget.Properties {
    
    
    // This class allows you to handle specific events on the settings class:
    //  The SettingChanging event is raised before a setting's value is changed.
    //  The PropertyChanged event is raised after the setting values are loaded.
    //  The SettingsLoaded event is raised after the setting values are loaded.
    //  The SettingsSaving event is raised before the setting values are saved.
    public sealed partial class Settings {
        
        public Settings() {
            // Mugen fork: keep Weather/Clock settings independent for every
            // named host instance launched with --profile <id>.
            if (!string.IsNullOrEmpty(global::HTCHome.Core.Environment.ProfileId)) {
                this.SettingsKey = global::HTCHome.Core.Environment.ProfileId;

                // Mugen profile defaults: desktop widgets should stay quiet and
                // should not create a separate taskbar button for every instance.
                if (this.Properties["EnableSounds"] != null) {
                    this.Properties["EnableSounds"].DefaultValue = false;
                }
                if (this.Properties["ShowIconOnTaskbar"] != null) {
                    this.Properties["ShowIconOnTaskbar"].DefaultValue = false;
                }
            }

            // // To add event handlers for saving and changing settings, uncomment the lines below:
            //
            // this.SettingChanging += this.SettingChangingEventHandler;
            //
            // this.SettingsSaving += this.SettingsSavingEventHandler;
            //
        }
        
        private void SettingChangingEventHandler(object sender, System.Configuration.SettingChangingEventArgs e) {
            // Add code to handle the SettingChangingEvent event here.
        }
        
        private void SettingsSavingEventHandler(object sender, System.ComponentModel.CancelEventArgs e) {
            // Add code to handle the SettingsSaving event here.
        }
    }
}
