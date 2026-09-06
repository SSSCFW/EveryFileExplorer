using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Tao.OpenGl;
using LibEveryFileExplorer._3D;
using System.Drawing.Imaging;
using LibEveryFileExplorer.Files;
using LibEveryFileExplorer;
using System.Runtime.InteropServices;
using _3DS.NintendoWare.GFX;

namespace _3DS.UI
{
	public partial class CGFXViewer : Form
	{
		ImageList ImageL;
		CMDLViewer ModViewer = null;
		ContextMenuStrip textureContextMenu;
		ToolStripMenuItem renameTextureMenuItem;

		CGFX mod;

		public CGFXViewer(CGFX mod)
		{
			this.mod = mod;
			InitializeComponent();
			Win32Util.SetWindowTheme(treeView1.Handle, "explorer", null);
			ImageL = new ImageList();
			ImageL.ColorDepth = ColorDepth.Depth32Bit;
			ImageL.ImageSize = new Size(16, 16);
			ImageL.Images.Add(Resource.jar);
			ImageL.Images.Add(Resource.point);
			ImageL.Images.Add(Resource.images_stack);
			ImageL.Images.Add(Resource.image_sunset);
			ImageL.Images.Add(Resource.films);
			ImageL.Images.Add(Resource.film);
			ImageL.Images.Add(Resource.tables_stacks);
			ImageL.Images.Add(Resource.table);
			ImageL.Images.Add(Resource.weather_clouds);
			ImageL.Images.Add(Resource.weather_cloud);
			ImageL.Images.Add(Resource.lighthouse_shine);
			ImageL.Images.Add(Resource.light_bulb);
			treeView1.ImageList = ImageL;

			InitializeTextureRename();
		}

		private void InitializeTextureRename()
		{
			treeView1.LabelEdit = true;
			treeView1.BeforeLabelEdit += treeView1_BeforeLabelEdit;
			treeView1.AfterLabelEdit += treeView1_AfterLabelEdit;
			treeView1.KeyDown += treeView1_KeyDown;
			treeView1.NodeMouseClick += treeView1_NodeMouseClick;

			textureContextMenu = new ContextMenuStrip(components);
			renameTextureMenuItem = new ToolStripMenuItem("Rename");
			renameTextureMenuItem.ShortcutKeys = Keys.F2;
			renameTextureMenuItem.Click += renameTextureMenuItem_Click;
			textureContextMenu.Items.Add(renameTextureMenuItem);
		}

		private bool IsRenamableTextureNode(TreeNode node)
		{
			return node != null &&
				node.Parent != null &&
				node.Parent.Text == "Textures" &&
				node.Tag is TXOB;
		}

		private void BeginRenameSelectedTexture()
		{
			TreeNode node = treeView1.SelectedNode;
			if (!IsRenamableTextureNode(node)) return;
			node.BeginEdit();
		}

		private void renameTextureMenuItem_Click(object sender, EventArgs e)
		{
			BeginRenameSelectedTexture();
		}

		private void treeView1_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.F2 && e.Modifiers == Keys.None)
			{
				BeginRenameSelectedTexture();
				e.Handled = true;
				e.SuppressKeyPress = true;
			}
		}

		private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
		{
			if (e.Button != MouseButtons.Right) return;

			treeView1.SelectedNode = e.Node;
			if (IsRenamableTextureNode(e.Node))
			{
				textureContextMenu.Show(treeView1, e.Location);
			}
		}

		private void treeView1_BeforeLabelEdit(object sender, NodeLabelEditEventArgs e)
		{
			if (!IsRenamableTextureNode(e.Node)) e.CancelEdit = true;
		}

		private void treeView1_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
		{
			if (e.Label == null) return;

			String error;
			if (!TryRenameTexture(e.Node, e.Label, out error))
			{
				e.CancelEdit = true;
				MessageBox.Show(this, error, "Rename Texture", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		private bool TryRenameTexture(TreeNode node, String newName, out String error)
		{
			error = null;
			if (!IsRenamableTextureNode(node))
			{
				error = "Only texture entries can be renamed.";
				return false;
			}

			if (String.IsNullOrEmpty(newName))
			{
				error = "Texture names cannot be empty.";
				return false;
			}

			for (int i = 0; i < newName.Length; i++)
			{
				if (newName[i] < 0x20 || newName[i] > 0x7E)
				{
					error = "Texture names must contain printable ASCII characters only.";
					return false;
				}
			}

			TXOB texture = (TXOB)node.Tag;
			if (String.Equals(texture.Name, newName, StringComparison.Ordinal)) return true;

			int textureIndex = Array.IndexOf(mod.Data.Textures, texture);
			if (textureIndex < 0)
			{
				error = "The selected texture could not be found in the CGFX texture table.";
				return false;
			}

			if (mod.Data.Dictionaries == null || mod.Data.Dictionaries.Length <= 1 || mod.Data.Dictionaries[1] == null || textureIndex >= mod.Data.Dictionaries[1].Count)
			{
				error = "The selected texture does not have a matching CGFX dictionary entry.";
				return false;
			}

			for (int i = 0; i < mod.Data.Textures.Length; i++)
			{
				if (i != textureIndex && String.Equals(mod.Data.Textures[i].Name, newName, StringComparison.Ordinal))
				{
					error = "A texture named \"" + newName + "\" already exists.";
					return false;
				}
			}

			DICT textureDictionary = mod.Data.Dictionaries[1];
			for (int i = 0; i < textureDictionary.Count; i++)
			{
				if (i != textureIndex && String.Equals(textureDictionary[i].Name, newName, StringComparison.Ordinal))
				{
					error = "A texture named \"" + newName + "\" already exists in the CGFX dictionary.";
					return false;
				}
			}

			String oldTextureName = texture.Name;
			String oldDictionaryName = textureDictionary[textureIndex].Name;
			texture.Name = newName;
			try
			{
				textureDictionary.Rename(textureIndex, newName);
			}
			catch (Exception ex)
			{
				texture.Name = oldTextureName;
				try
				{
					textureDictionary.Rename(textureIndex, oldDictionaryName);
				}
				catch
				{
				}
				error = "Failed to rebuild the CGFX texture dictionary: " + ex.Message;
				return false;
			}

			return true;
		}


		private void CGFX_Load(object sender, EventArgs e)
		{
			bool sel = false;
			treeView1.BeginUpdate();
			treeView1.Nodes.Clear();
			if (mod.Data.Models != null)
			{
				TreeNode section = new TreeNode("Models", 0, 0);
				treeView1.Nodes.Add(section);
				for (int i = 0; i < mod.Data.Models.Length; i++)
				{
					TreeNode entry = new TreeNode(mod.Data.Models[i].Name, 1, 1) { Tag = mod.Data.Models[i] };
					section.Nodes.Add(entry);
					if (i == 0 && !sel)
					{
						section.Expand();
						treeView1.SelectedNode = entry;
						sel = true;
					}
					//TODO: Content of CMDL
				}
			}
			if (mod.Data.Textures != null)
			{
				TreeNode section = new TreeNode("Textures", 2, 2);
				treeView1.Nodes.Add(section);
				for (int i = 0; i < mod.Data.Textures.Length; i++)
				{
					TreeNode entry = new TreeNode(mod.Data.Textures[i].Name, 3, 3) { Tag = mod.Data.Textures[i] };
					section.Nodes.Add(entry);
					if (i == 0 && !sel)
					{
						section.Expand();
						treeView1.SelectedNode = entry;
						sel = true;
					}
				}
			}
			if (mod.Data.Dictionaries[2] != null)
			{
				TreeNode section = new TreeNode("Lookup Tables", 6, 6);
				treeView1.Nodes.Add(section);
				for (int i = 0; i < mod.Data.Dictionaries[2].Count; i++)
				{
					TreeNode entry = new TreeNode(mod.Data.Dictionaries[2][i].Name, 7, 7);// { Tag = mod.Data.Textures[i] };
					section.Nodes.Add(entry);
				}
			}
			if (mod.Data.Dictionaries[6] != null)
			{
				TreeNode section = new TreeNode("Lights", 10, 10);
				treeView1.Nodes.Add(section);
				for (int i = 0; i < mod.Data.Dictionaries[6].Count; i++)
				{
					TreeNode entry = new TreeNode(mod.Data.Dictionaries[6][i].Name, 11, 11);// { Tag = mod.Data.Textures[i] };
					section.Nodes.Add(entry);
					if (i == 0 && !sel)
					{
						section.Expand();
						treeView1.SelectedNode = entry;
						sel = true;
					}
				}
			}
			if (mod.Data.Dictionaries[7] != null)
			{
				TreeNode section = new TreeNode("Fog", 8, 8);
				treeView1.Nodes.Add(section);
				for (int i = 0; i < mod.Data.Dictionaries[7].Count; i++)
				{
					TreeNode entry = new TreeNode(mod.Data.Dictionaries[7][i].Name, 9, 9);// { Tag = mod.Data.Textures[i] };
					section.Nodes.Add(entry);
					if (i == 0 && !sel)
					{
						section.Expand();
						treeView1.SelectedNode = entry;
						sel = true;
					}
				}
			}
			if (mod.Data.SkeletonAnimations != null)
			{
				TreeNode section = new TreeNode("Skeleton Animations", 4, 4);
				treeView1.Nodes.Add(section);
				for (int i = 0; i < mod.Data.SkeletonAnimations.Length; i++)
				{
					TreeNode entry = new TreeNode(mod.Data.SkeletonAnimations[i].Name, 5, 5) { Tag = mod.Data.SkeletonAnimations[i] };
					section.Nodes.Add(entry);
					if (i == 0 && !sel)
					{
						section.Expand();
						treeView1.SelectedNode = entry;
						sel = true;
					}
				}
			}
			if (mod.Data.MaterialAnimations != null)
			{
				TreeNode section = new TreeNode("Material Animations", 4, 4);
				treeView1.Nodes.Add(section);
				for (int i = 0; i < mod.Data.MaterialAnimations.Length; i++)
				{
					TreeNode entry = new TreeNode(mod.Data.MaterialAnimations[i].Name, 5, 5) { Tag = mod.Data.MaterialAnimations[i] };
					section.Nodes.Add(entry);
					if (i == 0 && !sel)
					{
						section.Expand();
						treeView1.SelectedNode = entry;
						sel = true;
					}
				}
			}
			if (mod.Data.VisibilityAnimations != null)
			{
				TreeNode section = new TreeNode("Visibility Animations", 4, 4);
				treeView1.Nodes.Add(section);
				for (int i = 0; i < mod.Data.VisibilityAnimations.Length; i++)
				{
					TreeNode entry = new TreeNode(mod.Data.VisibilityAnimations[i].Name, 5, 5) { Tag = mod.Data.VisibilityAnimations[i] };
					section.Nodes.Add(entry);
					if (i == 0 && !sel)
					{
						section.Expand();
						treeView1.SelectedNode = entry;
						sel = true;
					}
				}
			}
			treeView1.EndUpdate();
		}

		private void CGFX_Resize(object sender, EventArgs e)
		{
			if (ModViewer != null)
			{
				ModViewer.Render();
				ModViewer.Render();
			}
		}

		private void CGFX_Layout(object sender, LayoutEventArgs e)
		{
			if (ModViewer != null)
			{
				ModViewer.Render();
			}
		}

		private void tabControl1_Selecting(object sender, TabControlCancelEventArgs e)
		{
			if (ModViewer != null)
			{
				ModViewer.Render();
			}
		}

		private void NSBMDViewer_Activated(object sender, EventArgs e)
		{
			//render it multiple times to avoid glitches!
			if (ModViewer != null)
			{
				for (int i = 0; i < 8; i++) ModViewer.Render();
			}
		}

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			switch (e.Node.ImageIndex)
			{
				case 1:
					ModViewer = new CMDLViewer(mod, (CMDL)e.Node.Tag) { Dock = DockStyle.Fill };
					ModViewer.Width = panel1.Width;
					ModViewer.Height = panel1.Height;
					panel1.SuspendLayout();
					panel1.Controls.Add(ModViewer);
					if (panel1.Controls.Count > 1) panel1.Controls.RemoveAt(0);
					panel1.Invalidate();
					panel1.ResumeLayout();
					ModViewer.Invalidate();
					ModViewer.Render();
					ModViewer.Render();
					break;
				case 3:
					ModViewer = null;
					panel1.SuspendLayout();
					panel1.Controls.Add(new TXOBViewer((ImageTextureCtr)e.Node.Tag) { Dock = DockStyle.Fill, Width = panel1.Width, Height = panel1.Height });
					if (panel1.Controls.Count > 1) panel1.Controls.RemoveAt(0);
					panel1.Invalidate();
					panel1.ResumeLayout();
					break;
				default:
					ModViewer = null;
					panel1.SuspendLayout();
					panel1.Controls.Clear();
					panel1.Invalidate();
					panel1.ResumeLayout();
					break;
			}
		}

		private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
		{
			if (ModViewer != null)
			{
				ModViewer.Render();
			}
		}
	}
}
