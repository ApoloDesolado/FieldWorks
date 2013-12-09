// ---------------------------------------------------------------------------------------------
#region // Copyright (c) 2011, SIL International. All Rights Reserved.
// <copyright from='2011' to='2011' company='SIL International'>
//		Copyright (c) 2011, SIL International. All Rights Reserved.
//
//		Distributable under the terms of either the Common Public License or the
//		GNU Lesser General Public License, as specified in the LICENSING.txt file.
// </copyright>
#endregion
//
// File: FixLinksDlg.cs
// Responsibility: mcconnel
// ---------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using System.IO;
using System.Collections;
using SIL.FieldWorks.FDO;

namespace SIL.FieldWorks.FixData
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	///
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public partial class FixErrorsDlg : Form
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="T:FixLinksDlg"/> class.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public FixErrorsDlg()
		{
			InitializeComponent();
			m_btnFixLinks.Enabled = false;
			string ext = FdoFileHelper.ksFwDataXmlFileExtension;
			string lockext = ext + ".lock";
			foreach (var dir in Directory.GetDirectories(DirectoryFinder.ProjectsDirectory))
			{
				string basename = Path.GetFileName(dir);
				string datafile = Path.Combine(dir, basename + ext);
				string lockfile = Path.Combine(dir, basename + lockext);
				if (File.Exists(datafile) && !File.Exists(lockfile))
					m_lvProjects.Items.Add(basename);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Return the name of the selected project.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public string SelectedProject
		{
			get
			{
				if (m_lvProjects.CheckedItems.Count > 0)
					return m_lvProjects.CheckedItems[0].ToString();
				else
					return null;
			}
		}

		private void m_btnFixLinks_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.OK;
			this.Close();
		}

		private void m_btnCancel_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.Cancel;
			this.Close();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Allow only one item to be checked at a time.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_lvProjects_ItemCheck(object sender, ItemCheckEventArgs e)
		{
			if (m_lvProjects.SelectedIndex == e.Index)
			{
				IEnumerator ie = m_lvProjects.CheckedIndices.GetEnumerator();
				List<int> indexes = new List<int>();
				while (ie.MoveNext())
				{
					int idx = (int)ie.Current;
					if (idx != m_lvProjects.SelectedIndex)
						indexes.Add(idx);
				}
				foreach (var idx in indexes)
					m_lvProjects.SetItemChecked(idx, false);
				m_btnFixLinks.Enabled = (e.NewValue == CheckState.Checked);
			}
		}
	}
}