using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ASCOM.OnStep;

public class frmMain : Form
{
	private delegate void SetTextCallback(string text);

	private IContainer components;

	private Label label1;

	public frmMain()
	{
		InitializeComponent();
	}

	private void frmMain_Load(object sender, EventArgs e)
	{
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.label1 = new System.Windows.Forms.Label();
		base.SuspendLayout();
		this.label1.Location = new System.Drawing.Point(12, 10);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(199, 33);
		this.label1.TabIndex = 0;
		this.label1.Text = "This is an ASCOM driver, not a program for you to use.";
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(233, 52);
		base.Controls.Add(this.label1);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
		base.Name = "frmMain";
		this.Text = "OnStep Driver Server";
		base.ResumeLayout(false);
	}
}
