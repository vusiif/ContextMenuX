using ContextMenuX.Methods;
using System;
using System.Windows.Forms;

namespace ContextMenuX.Controls
{
    sealed class DetailedEditDialog : CommonDialog
    {
        public Guid GroupGuid { get; set; }

        public override void Reset() { }

        protected override bool RunDialog(IntPtr hwndOwner)
        {
            using(SubItemsForm frm = new SubItemsForm())
            using(DetailedEditList list = new DetailedEditList())
            {
                var location = GuidInfo.GetIconLocation(this.GroupGuid);
                frm.Icon = ResourceIcon.GetIcon(location.IconPath, location.IconIndex);
                frm.Text = AppString.Dialog.DetailedEdit.Replace("%s", GuidInfo.GetText(this.GroupGuid));
                frm.TopMost = AppConfig.TopMost;
                frm.AddList(list);
                list.GroupGuid = this.GroupGuid;
                list.UseUserDic = XmlDicHelper.DetailedEditGuidDic[this.GroupGuid];
                list.LoadItems();
                frm.ShowDialog();
            }
            return false;
        }
    }
}