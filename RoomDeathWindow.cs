using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LiveSplit.EscapeGoat2;
using System.Collections;
using LiveSplit.Model;

namespace LiveSplit.EscapeGoat2
{
    public partial class RoomDeathWindow : Form
    {
        public class RoomSorter : IComparer
        {
            public int sortCol;

            public RoomSorter() {
                sortCol = 1;
            }

            public int Compare(object a, object b) {
                ListViewItem lviA = (ListViewItem)a, lviB = (ListViewItem)b;
                Room roomA = (Room)lviA.Tag, roomB = (Room)lviB.Tag;
                int result = 0;

                if (sortCol != 0)
                    result = -String.CompareOrdinal(lviA.SubItems[sortCol].Text, lviB.SubItems[sortCol].Text);
                if (result != 0)
                    return result;

                // sorting on room or fallback
                int wingcomp = String.Compare(roomA.wing.ToString(), roomB.wing.ToString());
                if (wingcomp != 0)
                    return wingcomp;
                return (int)roomA.room < (int)roomB.room ? -1 : (int)roomA.room > (int)roomB.room ? 1 : 0;
            }
        }

        EscapeGoat2Component eg2Component;
        WorldMap map = new WorldMap();
        Dictionary<int, ListViewItem> items = new Dictionary<int, ListViewItem>();

        private RoomSorter roomSorter = new RoomSorter();

        public RoomDeathWindow(EscapeGoat2Component component) {
            InitializeComponent();
            listView1.ListViewItemSorter = roomSorter;

            eg2Component = component;

            FillRoomList();
        }

        public void FillRoomList() {
            bool anyrun = eg2Component.runCategory == "Any%";
            listView1.Items.Clear();
            items.Clear();
            foreach (var room in map.GetRooms()) {
                if (anyrun)
                {
                    if (new object[] { "S", 7 }.Contains(room.wing))
                        continue;
                    if (new object[] { 2, 3, 4, 5, 6, 8 }.Contains(room.wing) && (int)room.room > 5)
                        continue;
                    if (new object[] { 1, 9 }.Contains(room.wing) && (int)room.room > 6)
                        continue;
                }
                ListViewItem item = new ListViewItem(new string[] { room.ToString(), "0", "0", "0" });
                item.Tag = room;
                items[room.id] = listView1.Items.Add(item);
            }
        }

        public void UpdateDeathList(int? roomid = null) {
            if (roomid.HasValue) {
                if (items.ContainsKey(roomid.Value)) {
                    items[roomid.Value].SubItems[1].Text = eg2Component.runRoomDeaths[roomid.Value].ToString();
                    items[roomid.Value].SubItems[2].Text = eg2Component.sessionRoomDeaths[roomid.Value].ToString();
                    items[roomid.Value].SubItems[3].Text = eg2Component.totalRoomDeaths[roomid.Value].ToString();
                }
            } else {
                foreach (int i in eg2Component.totalRoomDeaths.Keys) {
                    if (!items.ContainsKey(i))
                        continue;
                    int r = 0, s = 0;
                    eg2Component.runRoomDeaths.TryGetValue(i, out r);
                    eg2Component.sessionRoomDeaths.TryGetValue(i, out s);
                    items[i].SubItems[1].Text = r.ToString();
                    items[i].SubItems[2].Text = s.ToString();
                    items[i].SubItems[3].Text = eg2Component.totalRoomDeaths[i].ToString();
                }
            }
        }

        private void RoomDeathWindow_FormClosing(object sender, FormClosingEventArgs e) {
            if (e.CloseReason == CloseReason.UserClosing) {
                Hide();
                e.Cancel = true;
            }
        }

        private void listView1_ColumnClick(object sender, ColumnClickEventArgs e) {
            roomSorter.sortCol = e.Column;
            listView1.Sort();
        }
    }
}
