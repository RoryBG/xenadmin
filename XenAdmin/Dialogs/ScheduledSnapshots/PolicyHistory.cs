﻿/* Copyright (c) Citrix Systems, Inc. 
 * All rights reserved. 
 * 
 * Redistribution and use in source and binary forms, 
 * with or without modification, are permitted provided 
 * that the following conditions are met: 
 * 
 * *   Redistributions of source code must retain the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer. 
 * *   Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer in the documentation and/or other 
 *     materials provided with the distribution. 
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using XenAdmin.Actions;
using XenAdmin.Alerts;
using XenAdmin.Controls;
using XenAdmin.Core;
using XenAdmin.Properties;
using XenAPI;

namespace XenAdmin.Dialogs.ScheduledSnapshots
{
    public partial class PolicyHistory : UserControl
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public PolicyHistory()
        {
            InitializeComponent();
            dataGridViewRunHistory.CellClick += new DataGridViewCellEventHandler(dataGridViewRunHistory_CellClick);
            ColumnExpand.DefaultCellStyle.NullValue = null;
            comboBoxTimeSpan.SelectedIndex = 0;
            dataGridViewRunHistory.Columns[2].ValueType = typeof(DateTime);
            dataGridViewRunHistory.Columns[2].DefaultCellStyle.Format = Messages.DATEFORMAT_DMY_HM;
        }



        void dataGridViewRunHistory_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                HistoryRow row = (HistoryRow)dataGridViewRunHistory.Rows[e.RowIndex];
                if (row.Alert.Type != "info")
                {
                    row.Expanded = !row.Expanded;
                    row.RefreshRow();
                }
            }
        }

        private VMSS _policy;

        private void StartRefreshTab()
        {
            /* hoursFromNow has 3 possible values:
                1) 0 -> top 10 messages (default)
                2) 24 -> messages from past 24 Hrs
                3) 7 * 24 -> messages from lst 7 days */

            var hoursFromNow = 0;
            switch (comboBoxTimeSpan.SelectedIndex)
            {
                case 0: /* default value*/
                    break;
                case 1:
                    hoursFromNow = 24;
                    break;
                case 2:
                    hoursFromNow = 7 * 24;
                    break;
            }

            var now = DateTime.Now;
            var alerts = VMSS.GetAlerts(_policy, hoursFromNow);
            log.DebugFormat("GetAlerts took: {0}", DateTime.Now - now);
            panelLoading.Visible = false;
            RefreshGrid(alerts);
        }

        public void RefreshTab(VMSS policy)
        {
            _policy = policy;
            if (_policy == null)
            {
                labelHistory.Text = "";
                comboBoxTimeSpan.Enabled = false;
            }
            else
            {
                comboBoxTimeSpan.Enabled = true;
                StartRefreshTab();
            }

        }

        private void RefreshGrid(List<PolicyAlert> alerts)
        {
            if (_policy != null)
            {
                ReloadHistoryLabel();
                dataGridViewRunHistory.Rows.Clear();
                var readOnlyAlerts = alerts.AsReadOnly();
                foreach (var alert in readOnlyAlerts)
                {
                    dataGridViewRunHistory.Rows.Add(new HistoryRow(alert));
                }
                dataGridViewRunHistory.Sort(ColumnDateTime, System.ComponentModel.ListSortDirection.Descending);
            }
        }



        public class HistoryRow : DataGridViewRow
        {
            private DataGridViewImageCell _expand = new DataGridViewImageCell();
            private DataGridViewTextAndImageCell _result = new DataGridViewTextAndImageCell();
            private DataGridViewTextBoxCell _dateTime = new DataGridViewTextBoxCell();
            private DataGridViewTextBoxCell _description = new DataGridViewTextBoxCell();
            public readonly PolicyAlert Alert;

            public HistoryRow(PolicyAlert alert)
            {
                Alert = alert;
                Cells.AddRange(_expand, _result, _dateTime, _description);
                RefreshRow();

            }

            [DefaultValue(false)]
            public bool Expanded { get; set; }

            public void RefreshRow()
            {

                _expand.Value = Expanded ? Resources.expanded_triangle : Resources.contracted_triangle;
                if (Alert.Type == "info")
                    _expand.Value = null;

                if (Alert.Type == "error")
                {
                    _result.Image = Properties.Resources._075_WarningRound_h32bit_16;
                    _result.Value = Messages.ERROR;
                }
                else if (Alert.Type == "warn")
                {
                    _result.Image = Properties.Resources._075_WarningRound_h32bit_16;
                    _result.Value = Messages.WARNING;
                }
                else if (Alert.Type == "info")
                {
                    _result.Image = Properties.Resources._075_TickRound_h32bit_16;
                    _result.Value = Messages.INFORMATION;
                }
                _dateTime.Value = Alert.Time;
                if (Alert.Type == "error")
                    _description.Value = Expanded ? string.Format("{0}\r\n{1}", Alert.ShortFormatBody, Alert.Text) : Alert.ShortFormatBody.Ellipsise(80);
                else
                    _description.Value = Expanded ? Alert.Text : Alert.ShortFormatBody.Ellipsise(90);
            }
        }


        public void Clear()
        {
            dataGridViewRunHistory.Rows.Clear();
        }

        private void comboBoxTimeSpan_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_policy != null)
            {
               StartRefreshTab();  
            }
        }

        private void ReloadHistoryLabel()
        {
            string Name;
            Name = _policy.Name();
            // ellipsise if necessary
            using (System.Drawing.Graphics g = labelHistory.CreateGraphics())
            {
                int maxWidth = labelShow.Left - labelHistory.Left;
                int availableWidth = maxWidth - (int)g.MeasureString(string.Format(Messages.HISTORY_FOR_POLICY, ""), labelHistory.Font).Width;
                Name = Name.Ellipsise(new System.Drawing.Rectangle(0, 0, availableWidth, labelHistory.Height), labelHistory.Font);
            }
            labelHistory.Text = string.Format(Messages.HISTORY_FOR_POLICY, Name);
        }
    }
}
