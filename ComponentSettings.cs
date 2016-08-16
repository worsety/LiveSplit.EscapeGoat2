using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.UI;

namespace LiveSplit.EscapeGoat2
{
    public partial class ComponentSettings : UserControl
    {
        public bool showDeathsRun { get; set; }
        public bool showDeathsSession { get; set; }
        public bool showDeathsTotal { get; set; }

        public ComponentSettings() {
            InitializeComponent();

            deathsRun.DataBindings.Add("Checked", this, "showDeathsRun", false, DataSourceUpdateMode.OnPropertyChanged);
            deathsSession.DataBindings.Add("Checked", this, "showDeathsSession", false, DataSourceUpdateMode.OnPropertyChanged);
            deathsTotal.DataBindings.Add("Checked", this, "showDeathsTotal", false, DataSourceUpdateMode.OnPropertyChanged);
        }

        public void SetSettings(XmlNode settings) {
            showDeathsRun = SettingsHelper.ParseBool(settings["ShowDeathsRun"], true);
            showDeathsSession = SettingsHelper.ParseBool(settings["ShowDeathsSession"], true);
            showDeathsTotal = SettingsHelper.ParseBool(settings["ShowDeathsTotal"], true);
        }

        public XmlNode GetSettings(XmlDocument document) {
            XmlElement root = document.CreateElement("Settings");

            SettingsHelper.CreateSetting(document, root, "ShowDeathsRun", showDeathsRun);
            SettingsHelper.CreateSetting(document, root, "ShowDeathsSession", showDeathsSession);
            SettingsHelper.CreateSetting(document, root, "ShowDeathsTotal", showDeathsTotal);
            return root;
        }
    }
}
