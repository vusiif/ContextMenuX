using ContextMenuX.Controls;
using ContextMenuX.Methods;
using System.Windows.Forms;

namespace ContextMenuX.Controls.Interfaces
{
    interface IBtnDeleteItem
    {
        DeleteButton BtnDelete { get; set; }
        void DeleteMe();
    }

    sealed class DeleteButton : PictureButton
    {
        public DeleteButton(IBtnDeleteItem item) : base(AppImage.Delete)
        {
            MyListItem listItem = (MyListItem)item;
            listItem.AddCtr(this);
            this.MouseDown += (sender, e) =>
            {
                if(AppMessageBox.Show(AppString.Message.ConfirmDelete, MessageBoxButtons.YesNo) == DialogResult.Yes) item.DeleteMe();
            };
        }
    }
}