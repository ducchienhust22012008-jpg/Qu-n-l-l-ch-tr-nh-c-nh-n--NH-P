using System.Drawing;
using System.Windows.Forms;

namespace Nhip {
    public class BrandedForm : Form {
        readonly Icon brandIcon;
        public BrandedForm() {
            using(var stream=typeof(BrandedForm).Assembly.GetManifestResourceStream("Nhip.App.ico"))
            using(var loaded=new Icon(stream)) brandIcon=(Icon)loaded.Clone();
            Icon=brandIcon;
        }
        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            if(disposing && brandIcon!=null)brandIcon.Dispose();
        }
    }
}
