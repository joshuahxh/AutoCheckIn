using System;
using System.Windows.Forms;

namespace AutoCheckIn
{
    public partial class EditForm : Form
    {
        public ListViewItem lvi_Edit;

        public EditForm()
        {
            InitializeComponent();
        }

        private void EditForm_Load(object sender, EventArgs e)
        {
            if (lvi_Edit != null)
            {
                tb_Confirmation.Text = lvi_Edit.SubItems[0].Text;
                tb_FirstName.Text = lvi_Edit.SubItems[1].Text;
                tb_LastName.Text = lvi_Edit.SubItems[2].Text;
                tb_Times.Text = lvi_Edit.SubItems[3].Text;

                btn_Add.Text = "Update";
                this.Text = "Change AIR Itinerary";
            }
            else
            {
                tb_LastName.Text = Properties.Settings.Default.lastName;
                tb_FirstName.Text = Properties.Settings.Default.firstName;
                btn_Add.Text = "Add";
                this.Text = "Add AIR Itinerary";
            }
        }

        private void btn_Add_Click(object sender, EventArgs e)
        {
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Close();
        }
    }
}
