﻿using System;
using System.Text.RegularExpressions;
using System.Windows;
using TxEditor.ViewModel;
using Unclassified.UI;
using TxLib;

namespace TxEditor.View
{
	public partial class TextKeyWindow : Window
	{
		public TextKeyWindow()
		{
			InitializeComponent();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			this.HideIcon();
			base.OnSourceInitialized(e);
		}

		public string TextKey
		{
			get { return TextKeyText.Text.Trim(); }
			set { TextKeyText.Text = value != null ? value.Trim() : null; }
		}

		public bool RenameSelectMode { get; set; }

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			//TextKeyText.SelectAll();
			if (!RenameSelectMode)
			{
				Match m = Regex.Match(TextKey, @"^((?:.*?:)?(?:[^.]*\.)*)([^.]*[:.])$");
				if (m.Success)
				{
					TextKeyText.SelectionStart = m.Groups[1].Length;
					TextKeyText.SelectionLength = m.Groups[2].Length;
				}
			}
			else
			{
				Match m = Regex.Match(TextKey, @"^((?:.*?:)?(?:[^.]*\.)*)([^.]*)$");
				if (m.Success)
				{
					TextKeyText.SelectionStart = m.Groups[1].Length;
					TextKeyText.SelectionLength = m.Groups[2].Length;
				}
			}
			TextKeyText.Focus();
		}

		private void OKButton_Click(object sender, RoutedEventArgs e)
		{
			string errorMessage;
			if (!TextKeyViewModel.ValidateName(TextKey, out errorMessage))
			{
				MessageBox.Show(
					Tx.T("msg.invalid text key entered", "msg", errorMessage),
					Tx.T("msg.caption.error"),
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}
			
			DialogResult = true;
			Close();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}
	}
}
