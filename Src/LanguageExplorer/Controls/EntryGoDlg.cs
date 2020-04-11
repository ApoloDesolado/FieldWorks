// Copyright (c) 2009-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml.Linq;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainServices;

namespace LanguageExplorer.Controls
{
	internal class EntryGoDlg : BaseGoDlg
	{
		protected bool m_fNewlyCreated;
		protected ILexSense m_newSense;
		protected ILexEntry m_startingEntry;
		protected int m_oldSearchWs;

		#region Properties

		protected override WindowParams DefaultWindowParams => new WindowParams { m_title = LanguageExplorerControls.ksFindLexEntry };

		protected override string PersistenceLabel => "EntryGo";

		/// <summary>
		/// Get/Set the starting entry object.  This will not be displayed in the list of
		/// matching entries.
		/// </summary>
		public ILexEntry StartingEntry
		{
			get => (ILexEntry)m_matchingObjectsBrowser.StartingObject;
			set => m_matchingObjectsBrowser.StartingObject = value;
		}

		protected override string Form
		{
			set => base.Form = MorphServices.EnsureNoMarkers(value, m_cache);
		}

		#endregion Properties

		#region	Construction and Destruction

		/// <summary />
		public EntryGoDlg()
		{
			SetHelpTopic("khtpFindInDictionary"); // Default help topic ID
			m_objectsLabel.Text = LanguageExplorerControls.ksLexicalEntries;
		}

		protected override void InitializeMatchingObjects()
		{
			var searchEngine = SearchEngine.Get(PropertyTable, "EntryGoSearchEngine", () => new EntryGoSearchEngine(m_cache));
			m_matchingObjectsBrowser.Initialize(m_cache, FwUtils.StyleSheetFromPropertyTable(PropertyTable), XDocument.Parse(LanguageExplorerResources.MatchingEntriesParameters).Root, searchEngine);
			m_matchingObjectsBrowser.ColumnsChanged += m_matchingObjectsBrowser_ColumnsChanged;
			// start building index
			var selectedWs = (CoreWritingSystemDefinition)m_cbWritingSystems.SelectedItem;
			if (selectedWs != null)
			{
				m_matchingObjectsBrowser.SearchAsync(GetFields(string.Empty, selectedWs.Handle));
			}
		}
		#endregion Construction and Destruction

		#region	Other methods

		/// <summary>
		/// Reset the list of matching items.
		/// </summary>
		protected override void ResetMatches(string searchKey)
		{
			var mmt = MorphServices.GetTypeIfMatchesPrefix(m_cache, searchKey, out _);
			if (mmt != null)
			{
				searchKey = string.Empty;
				m_btnInsert.Enabled = false;
			}
			else if (searchKey.Length > 0)
			{
				// NB: This method strips off reserved characters for searchKey,
				// which is a good thing.  (fixes LT-802?)
				try
				{
					MorphServices.FindMorphType(m_cache, ref searchKey, out _);
					m_btnInsert.Enabled = searchKey.Length > 0;
				}
				catch (Exception ex)
				{
					Cursor = Cursors.Default;
					MessageBox.Show(ex.Message, LanguageExplorerControls.ksInvalidForm, MessageBoxButtons.OK);
					m_btnInsert.Enabled = false;
					return;
				}
			}
			else
			{
				m_btnInsert.Enabled = false;
			}
			var selectedWs = (CoreWritingSystemDefinition)m_cbWritingSystems.SelectedItem;
			var wsSelHvo = selectedWs?.Handle ?? 0;

			if (!m_vernHvos.Contains(wsSelHvo) && !m_analHvos.Contains(wsSelHvo))
			{
				wsSelHvo = TsStringUtils.GetWsAtOffset(m_tbForm.Tss, 0);
				if (!m_vernHvos.Contains(wsSelHvo) && !m_analHvos.Contains(wsSelHvo))
				{
					return;
				}
			}
			if (m_oldSearchKey == searchKey && m_oldSearchWs == wsSelHvo)
			{
				return; // Nothing new to do, so skip it.
			}
			if (m_oldSearchKey != string.Empty || searchKey != string.Empty)
			{
				StartSearchAnimation();
			}
			// disable Go button until we rebuild our match list.
			m_btnOK.Enabled = false;
			m_oldSearchKey = searchKey;
			m_oldSearchWs = wsSelHvo;
			m_matchingObjectsBrowser.SearchAsync(GetFields(searchKey, wsSelHvo));
		}

		protected IEnumerable<SearchField> GetFields(string str, int ws)
		{
			var tssKey = TsStringUtils.MakeString(str, ws);
			if (m_vernHvos.Contains(ws))
			{
				if (m_matchingObjectsBrowser.IsVisibleColumn("EntryHeadword") || m_matchingObjectsBrowser.IsVisibleColumn("CitationForm"))
				{
					yield return new SearchField(LexEntryTags.kflidCitationForm, tssKey);
				}
				if (m_matchingObjectsBrowser.IsVisibleColumn("EntryHeadword") || m_matchingObjectsBrowser.IsVisibleColumn("LexemeForm"))
				{
					yield return new SearchField(LexEntryTags.kflidLexemeForm, tssKey);
				}
				if (m_matchingObjectsBrowser.IsVisibleColumn("Allomorphs"))
				{
					yield return new SearchField(LexEntryTags.kflidAlternateForms, tssKey);
				}
			}
			if (m_analHvos.Contains(ws))
			{
				if (m_matchingObjectsBrowser.IsVisibleColumn("Glosses"))
				{
					yield return new SearchField(LexSenseTags.kflidGloss, tssKey);
				}
				if (m_matchingObjectsBrowser.IsVisibleColumn("Definitions"))
				{
					yield return new SearchField(LexSenseTags.kflidDefinition, tssKey);
				}
			}
		}

		private void m_matchingObjectsBrowser_ColumnsChanged(object sender, EventArgs e)
		{
			if (m_oldSearchKey != string.Empty && m_oldSearchWs != 0)
			{
				var tempKey = m_oldSearchKey;
				m_oldSearchKey = string.Empty;
				ResetMatches(tempKey); // force Reset w/o changing strings
			}
		}

		#endregion  // Other methods

		#region	Event handlers

		protected override string AdjustText(out int addToSelection)
		{
			var selWasAtEnd = m_tbForm.SelectionStart + m_tbForm.SelectionLength == m_tbForm.Text.Length;
			var fixedText = base.AdjustText(out addToSelection);
			// Only do the morpheme marker trick if the selection is at the end, a good sign the user just
			// typed it. This avoids the situation where it is impossible to delete one of a pair of tildes.
			if (!selWasAtEnd)
			{
				return fixedText;
			}
			// Check whether we need to handle partial marking of a morphtype (suprafix in the
			// default case: see LT-6082).
			var mmt = MorphServices.GetTypeIfMatchesPrefix(m_cache, fixedText, out var sAdjusted);
			if (mmt != null && fixedText != sAdjusted)
			{
				m_skipCheck = true;
				m_tbForm.Text = sAdjusted;
				m_skipCheck = false;
				return sAdjusted;
			}
			return fixedText;
		}

		protected override void m_btnInsert_Click(object sender, EventArgs e)
		{
			using (var dlg = new InsertEntryDlg())
			{
				dlg.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
				var form = m_tbForm.Text.Trim();
				var tssFormTrimmed = TsStringUtils.MakeString(form, TsStringUtils.GetWsAtOffset(m_tbForm.Tss, 0));
				dlg.SetDlgInfo(m_cache, tssFormTrimmed);
				if (dlg.ShowDialog(this) == DialogResult.OK)
				{
					dlg.GetDialogInfo(out var entry, out m_fNewlyCreated);
					m_selObject = entry;
					if (m_fNewlyCreated)
					{
						m_newSense = entry.SensesOS[0];
					}
					// If we ever decide not to simulate the btnOK click at this point, then
					// the new sense id will need to be handled by a subclass differently (ie,
					// being added to the list of senses maintained by LinkEntryOrSenseDlg,
					// the selected index into that list also being changed).
					HandleMatchingSelectionChanged();
					if (m_btnOK.Enabled)
					{
						m_btnOK.PerformClick();
					}
				}
			}
		}
		#endregion  // Event handlers
	}
}