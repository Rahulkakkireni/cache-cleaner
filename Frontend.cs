using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClearSpace {
    static class Style {
        public static readonly bool Contrast = SystemInformation.HighContrast;
        public static Color Background = Contrast ? SystemColors.Control : Color.FromArgb(245,245,247);
        public static Color Surface = Contrast ? SystemColors.Window : Color.White;
        public static Color Ink = Contrast ? SystemColors.WindowText : Color.FromArgb(29,29,31);
        public static Color Muted = Contrast ? SystemColors.WindowText : Color.FromArgb(101,101,110);
        public static Color Blue = Contrast ? SystemColors.Highlight : Color.FromArgb(0,105,225);
        public static Color Line = Contrast ? SystemColors.WindowText : Color.FromArgb(231,232,237);
        public static Color Soft = Contrast ? SystemColors.Control : Color.FromArgb(240,241,245);
        public static Font Type(float size, bool bold=false) { return new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular); }
        public static GraphicsPath Round(RectangleF r,float radius) {
            float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));var p=new GraphicsPath();
            p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
        public static void Text(Graphics g,string value,Font font,Color color,Rectangle bounds,ContentAlignment align) {
            TextFormatFlags flags=TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis;
            if(align==ContentAlignment.MiddleRight) flags|=TextFormatFlags.Right|TextFormatFlags.VerticalCenter;
            else if(align==ContentAlignment.MiddleCenter) flags|=TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter;
            else flags|=TextFormatFlags.Left|TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g,value,font,bounds,color,flags);
        }
        public static Label Label(string text,float size,bool bold=false) { return new Label { Text=text,Font=Type(size,bold),ForeColor=bold?Ink:Muted,BackColor=Color.Transparent,AutoEllipsis=true }; }
        public static void Tile(Graphics g,Rectangle rect,int kind) {
            Color[] colors={Blue,Color.FromArgb(113,82,190),Color.FromArgb(201,115,29),Color.FromArgb(16,128,116)};
            Color color=Contrast?Ink:colors[kind%4];using(var path=Round(rect,10))using(var b=new SolidBrush(Contrast?Surface:Color.FromArgb(24,color)))g.FillPath(b,path);
            float x=rect.X,y=rect.Y,w=rect.Width;using(var pen=new Pen(color,1.7f)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;
                if(kind<2){g.DrawRectangle(pen,x+w*.3f,y+w*.23f,w*.4f,w*.53f);g.DrawLine(pen,x+w*.39f,y+w*.4f,x+w*.6f,y+w*.4f);g.DrawLine(pen,x+w*.39f,y+w*.53f,x+w*.6f,y+w*.53f);}
                else {g.DrawEllipse(pen,x+w*.23f,y+w*.23f,w*.54f,w*.54f);g.DrawEllipse(pen,x+w*.39f,y+w*.39f,w*.22f,w*.22f);g.DrawLine(pen,x+w*.24f,y+w*.5f,x+w*.38f,y+w*.5f);g.DrawLine(pen,x+w*.61f,y+w*.5f,x+w*.75f,y+w*.5f);}
            }
        }
    }
    class Surface : Panel {
        public Surface(){DoubleBuffered=true;BackColor=Style.Background;Padding=new Padding(20);}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Style.Round(new RectangleF(1,1,Width-3,Height-4),18))using(var brush=new SolidBrush(Style.Surface)){e.Graphics.FillPath(brush,path);if(Style.Contrast)using(var pen=new Pen(Style.Line))e.Graphics.DrawPath(pen,path);}
            base.OnPaint(e);
        }
    }
    class SoftButton : Button {
        public bool Primary,Destructive,Chosen;bool hover,pressed;
        public SoftButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;Font=Style.Type(10,true);Size=new Size(148,42);UseVisualStyleBackColor=false;BackColor=Style.Background;}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){pressed=e.Button==MouseButtons.Left;Invalidate();base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){pressed=true;Invalidate();}base.OnKeyDown(e);}
        protected override void OnKeyUp(KeyEventArgs e){pressed=false;Invalidate();base.OnKeyUp(e);}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            bool solid=Primary||Chosen;Color baseColor=Destructive?Color.FromArgb(199,42,49):Style.Blue;
            Color fill=!Enabled?Style.Soft:solid?baseColor:Style.Soft;
            if(Enabled&&(hover||pressed)){float weight=pressed?.84f:.94f;fill=Color.FromArgb((int)(fill.R*weight),(int)(fill.G*weight),(int)(fill.B*weight));}
            Rectangle r=new Rectangle(1,pressed?2:1,Width-3,Height-3);using(var path=Style.Round(r,11))using(var brush=new SolidBrush(fill)){g.FillPath(brush,path);if(Style.Contrast)using(var pen=new Pen(Style.Ink))g.DrawPath(pen,path);}
            Style.Text(g,Text,Font,!Enabled?Style.Muted:solid?SystemColors.HighlightText:Style.Ink,r,ContentAlignment.MiddleCenter);
            if(Focused&&ShowFocusCues)using(var path=Style.Round(new Rectangle(4,4,Width-9,Height-9),8))using(var pen=new Pen(solid?Color.White:Style.Blue,1.5f)){pen.DashStyle=DashStyle.Dot;g.DrawPath(pen,path);}
        }
    }
    class Overview : Control {
        public string Metric="Ready to scan", Caption="A fresh start for your PC", Detail="Scan to see how much older cache you can remove.";
        public double Fraction;public bool HasScan,Working;
        public Overview(){DoubleBuffered=true;BackColor=Style.Background;AccessibleRole=AccessibleRole.StaticText;}
        public void RefreshMetric(){AccessibleName=Caption+". "+Metric+". "+Detail;Invalidate();}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Style.Round(new RectangleF(1,1,Width-3,Height-4),18))using(var b=new SolidBrush(Style.Surface)){g.FillPath(b,path);if(Style.Contrast)using(var pen=new Pen(Style.Line))g.DrawPath(pen,path);}
            int split=(int)(Width*.52);using(var title=Style.Type(23,true))using(var body=Style.Type(10))using(var metric=Style.Type(HasScan?32:22,true))using(var small=Style.Type(9,true)) {
                Style.Text(g,"Less clutter. More space.",title,Style.Ink,new Rectangle(24,18,split-35,44),ContentAlignment.MiddleLeft);
                Style.Text(g,"Review your cache. Choose what goes.",body,Style.Muted,new Rectangle(25,65,split-35,24),ContentAlignment.MiddleLeft);
                Style.Text(g,"Your personal files stay right where they are.",body,Style.Muted,new Rectangle(25,91,split-35,24),ContentAlignment.MiddleLeft);
                int metricWidth=Width-split-128;
                Style.Text(g,Caption,small,Style.Muted,new Rectangle(split,18,metricWidth,22),ContentAlignment.MiddleLeft);
                Style.Text(g,Metric,metric,Style.Ink,new Rectangle(split,42,metricWidth,50),ContentAlignment.MiddleLeft);
                Style.Text(g,Detail,body,Style.Muted,new Rectangle(split,94,metricWidth+4,28),ContentAlignment.MiddleLeft);
            }
            Rectangle ring=new Rectangle(Width-114,26,86,86);using(var pen=new Pen(Style.Soft,7)){g.DrawArc(pen,ring,0,360);}if(HasScan&&Fraction>0)using(var pen=new Pen(Style.Blue,7)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;g.DrawArc(pen,ring,-90,(float)(360*Math.Min(1,Fraction)));}
            using(var pen=new Pen(Style.Blue,2.2f)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;
                float x=ring.X+43,y=ring.Y+43;g.DrawLine(pen,x,y-14,x,y+12);g.DrawLine(pen,x-8,y+4,x,y+12);g.DrawLine(pen,x+8,y+4,x,y+12);g.DrawLine(pen,x-13,y+19,x+13,y+19);}
        }
    }
    class CacheRow : Panel {
        public CheckBox Check=new CheckBox();public Category Category;public int Kind;public bool HasScan;
        public string State="Not scanned";readonly ToolTip tip=new ToolTip();
        public CacheRow(Category category,int kind){Category=category;Kind=kind;Height=66;BackColor=Style.Surface;DoubleBuffered=true;
            Check.AutoSize=false;Check.Size=new Size(24,24);Check.Location=new Point(18,21);Check.AccessibleName="Include "+category.Name;Check.TabIndex=0;Controls.Add(Check);tip.SetToolTip(this,category.Description);Resize+=(s,e)=>Invalidate();Check.CheckedChanged+=(s,e)=>Invalidate();}
        protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            if(Check.Checked){using(var b=new SolidBrush(Style.Contrast?Style.Surface:Color.FromArgb(248,250,255)))g.FillRectangle(b,0,0,Width,Height);}
            Style.Tile(g,new Rectangle(51,13,40,40),Kind);
            string[] names={"Temporary files","Windows temporary files","Chrome cache","Edge cache"};string[] descriptions={"Older files in your temp folder","Accessible system temp files","Cached pages and browser resources","Cached pages and browser resources"};
            int available=Width-260;using(var title=Style.Type(10,true))using(var small=Style.Type(9)){
                Style.Text(g,names[Kind],title,Style.Ink,new Rectangle(104,10,available,24),ContentAlignment.MiddleLeft);
                Style.Text(g,HasScan?Category.Files.Count.ToString("N0")+(Category.Files.Count==1?" file · ":" files · ")+State:descriptions[Kind],small,Style.Muted,new Rectangle(104,34,available+15,22),ContentAlignment.MiddleLeft);
                Style.Text(g,HasScan?Engine.Size(Category.Bytes):"—",title,Style.Ink,new Rectangle(Width-135,17,110,30),ContentAlignment.MiddleRight);
            }
            if(Kind<3)using(var pen=new Pen(Style.Line))g.DrawLine(pen,104,Height-1,Width-22,Height-1);
        }
    }
    public class MainWindow : Form {
        List<Category> categories;readonly List<CacheRow> rows=new List<CacheRow>();readonly List<SoftButton> ages=new List<SoftButton>();
        Overview overview=new Overview();Label status=Style.Label("Ready when you are. Start with a scan.",9),listSummary=Style.Label("Select the locations you want to include.",9);
        SoftButton scan=new SoftButton {Text="Scan PC"},clean=new SoftButton {Text="Clean selected",Primary=true},preview=new SoftButton {Text="Preview selected files"},cancel=new SoftButton {Text="Stop",Visible=false};
        ProgressBar progress=new ProgressBar();Panel rowHost=new Panel();int ageIndex;volatile bool cancelled;bool busy,scanned;DateTime cutoff;
        public MainWindow(){
            Text="ClearSpace — Cache Cleaner";Size=new Size(1040,790);MinimumSize=new Size(960,720);StartPosition=FormStartPosition.CenterScreen;BackColor=Style.Background;ForeColor=Style.Ink;Font=Style.Type(10);AutoScaleMode=AutoScaleMode.Dpi;
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=5};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,146));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,62));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,28));Controls.Add(layout);
            var header=new Panel {Dock=DockStyle.Fill};var brand=Style.Label("ClearSpace",17,true);brand.SetBounds(45,0,210,28);header.Controls.Add(brand);var sub=Style.Label("Windows cache cleaner",9);sub.SetBounds(46,29,250,20);header.Controls.Add(sub);
            header.Paint+=(s,e)=>{var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Style.Round(new Rectangle(1,3,34,34),10))using(var brush=new SolidBrush(Style.Blue))g.FillPath(brush,path);using(var pen=new Pen(Color.White,1.8f)){g.DrawArc(pen,9,10,17,17,40,290);g.DrawLine(pen,23,12,26,12);g.DrawLine(pen,26,12,26,8);}};
            var privateLabel=Style.Label("LOCAL  ·  PRIVATE",9,true);privateLabel.TextAlign=ContentAlignment.MiddleRight;privateLabel.Dock=DockStyle.Right;privateLabel.Width=180;header.Controls.Add(privateLabel);layout.Controls.Add(header,0,0);
            overview.Dock=DockStyle.Fill;overview.Margin=new Padding(0,4,0,10);layout.Controls.Add(overview,0,1);
            var columns=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,66));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,34));layout.Controls.Add(columns,0,2);
            var locations=new Surface {Dock=DockStyle.Fill,Margin=new Padding(0,0,12,0),Padding=Padding.Empty};columns.Controls.Add(locations,0,0);
            var locationTitle=Style.Label("Cache locations",12,true);locationTitle.SetBounds(21,13,270,28);locations.Controls.Add(locationTitle);listSummary.SetBounds(22,42,520,24);listSummary.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;locations.Controls.Add(listSummary);
            rowHost.Dock=DockStyle.None;rowHost.Height=264;rowHost.BackColor=Style.Surface;rowHost.Padding=Padding.Empty;rowHost.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;locations.Controls.Add(rowHost);
            locations.Resize+=(s,e)=>{rowHost.SetBounds(2,75,locations.Width-5,264);};
            var options=new Surface {Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=Padding.Empty};columns.Controls.Add(options,1,0);
            var optionTitle=Style.Label("Keep recent files",12,true);optionTitle.SetBounds(20,16,260,25);options.Controls.Add(optionTitle);var optionSub=Style.Label("Only remove files older than",9);optionSub.SetBounds(21,45,275,24);options.Controls.Add(optionSub);
            var ageLayout=new TableLayoutPanel {ColumnCount=3,RowCount=1,Location=new Point(19,79),Height=39,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,Width=268,BackColor=Style.Surface};
            for(int i=0;i<3;i++)ageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));options.Controls.Add(ageLayout);
            string[] names={"24 hours","7 days","30 days"};for(int i=0;i<3;i++){int index=i;var choice=new SoftButton {Text=names[i],Chosen=i==0,Dock=DockStyle.Fill,Margin=new Padding(2),Font=Style.Type(9,true),AccessibleName="Minimum file age: "+names[i]};choice.Click+=(s,e)=>ChooseAge(index);ages.Add(choice);ageLayout.Controls.Add(choice,i,0);}
            var divider=new Panel {BackColor=Style.Line,Location=new Point(21,139),Height=1,Width=260,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};options.Controls.Add(divider);
            var protectedTitle=Style.Label("Just cache. Nothing personal.",10,true);protectedTitle.SetBounds(21,156,278,26);options.Controls.Add(protectedTitle);
            var protectedText=Style.Label("Documents, photos and downloads stay.\nCookies, history and passwords stay.\nRecent and locked files are skipped.",9);protectedText.SetBounds(21,188,278,65);options.Controls.Add(protectedText);
            preview.SetBounds(20,272,274,42);preview.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;options.Controls.Add(preview);
            options.Resize+=(s,e)=>{ageLayout.Width=options.Width-40;divider.Width=options.Width-42;protectedTitle.Width=options.Width-42;protectedText.Width=options.Width-42;preview.SetBounds(20,options.Height-61,options.Width-40,42);};
            var footer=new Panel {Dock=DockStyle.Fill,Margin=Padding.Empty};layout.Controls.Add(footer,0,3);
            var deletion=Style.Label("You review. You decide.",10,true);deletion.SetBounds(1,12,300,25);footer.Controls.Add(deletion);var irreversible=Style.Label("Cleaning permanently removes the selected files.",9);irreversible.SetBounds(1,37,450,22);footer.Controls.Add(irreversible);
            clean.SetBounds(810,13,167,42);clean.Anchor=AnchorStyles.Top|AnchorStyles.Right;scan.SetBounds(641,13,151,42);scan.Anchor=AnchorStyles.Top|AnchorStyles.Right;cancel.SetBounds(538,13,88,42);cancel.Anchor=AnchorStyles.Top|AnchorStyles.Right;footer.Controls.AddRange(new Control[]{cancel,scan,clean});footer.Resize+=(s,e)=>{clean.Left=footer.Width-clean.Width;scan.Left=clean.Left-scan.Width-12;cancel.Left=scan.Left-cancel.Width-12;};
            var statusPanel=new Panel {Dock=DockStyle.Fill};layout.Controls.Add(statusPanel,0,4);status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;statusPanel.Controls.Add(status);progress.Dock=DockStyle.Right;progress.Width=110;progress.Height=8;progress.Style=ProgressBarStyle.Continuous;progress.AccessibleName="Cache locations scanned";progress.Visible=false;statusPanel.Controls.Add(progress);
            categories=Engine.Categories();Populate(false);clean.Enabled=false;preview.Enabled=false;scan.Click+=async(s,e)=>await DoScan();clean.Click+=async(s,e)=>await DoClean();preview.Click+=(s,e)=>{using(var dialog=CreatePreview())dialog.ShowDialog(this);};cancel.Click+=(s,e)=>{cancelled=true;status.Text="Stopping after the current file…";};
            AcceptButton=scan;Shown+=(s,e)=>ArrangeRows();Resize+=(s,e)=>ArrangeRows();FormClosing+=(s,e)=>{if(busy){cancelled=true;e.Cancel=true;status.Text="Stopping safely. Close the window once the operation finishes.";}};
        }
        void ArrangeRows(){if(rowHost.Parent==null)return;rowHost.SetBounds(2,75,rowHost.Parent.Width-5,264);for(int i=0;i<rows.Count;i++)rows[i].SetBounds(0,i*66,rowHost.Width,66);}
        void ChooseAge(int index){if(busy)return;ageIndex=index;for(int i=0;i<ages.Count;i++){ages[i].Chosen=i==index;ages[i].Invalidate();}scanned=false;clean.Enabled=false;preview.Enabled=false;foreach(var r in rows){r.HasScan=false;r.Invalidate();}overview.HasScan=false;overview.Metric="Scan to refresh";overview.Caption="Minimum age updated";overview.Detail="Choose Scan PC to update the results.";overview.RefreshMetric();status.Text="Minimum age updated. Scan again before cleaning.";}
        void SetBusy(bool value){busy=value;scan.Enabled=!value;rowHost.Enabled=!value;foreach(var a in ages)a.Enabled=!value;cancel.Visible=value;cancel.Enabled=value;progress.Visible=value;clean.Enabled=!value&&scanned&&Selected().Any(c=>c.Files.Count>0);preview.Enabled=!value&&scanned&&Selected().Any(c=>c.Files.Count>0);overview.Working=value;overview.RefreshMetric();}
        List<Category> Selected(){return rows.Where(r=>r.Check.Checked).Select(r=>r.Category).ToList();}
        void UpdateAmount(){if(!scanned)return;var chosen=Selected();long bytes=chosen.Sum(c=>c.Bytes),total=categories.Sum(c=>c.Bytes);int files=chosen.Sum(c=>c.Files.Count);overview.HasScan=true;overview.Caption="Selected cache";overview.Metric=Engine.Size(bytes);overview.Detail=files.ToString("N0")+(files==1?" file selected":" files selected");overview.Fraction=total>0?(double)bytes/total:0;overview.RefreshMetric();clean.Enabled=!busy&&files>0;preview.Enabled=!busy&&files>0;listSummary.Text=chosen.Count+" of "+categories.Count+" locations selected";}
        void Populate(bool select){foreach(Control row in rowHost.Controls.Cast<Control>().ToArray()){rowHost.Controls.Remove(row);row.Dispose();}rows.Clear();for(int i=0;i<categories.Count;i++){var c=categories[i];var row=new CacheRow(c,i){Left=0,Top=i*66,Width=rowHost.Width,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,HasScan=scanned,State=Engine.Running(c.ProcessName)?"Close browser to scan":c.Skipped>0?c.Skipped+" skipped":"Ready"};row.Check.Checked=select&&c.Files.Count>0;row.Check.AccessibleDescription=row.State+". "+c.Files.Count+" files. "+Engine.Size(c.Bytes);row.Check.CheckedChanged+=(s,e)=>UpdateAmount();rows.Add(row);rowHost.Controls.Add(row);}ArrangeRows();if(scanned)UpdateAmount();}
        public async Task DoScan(){cancelled=false;scanned=false;SetBusy(true);overview.HasScan=false;overview.Caption="Read-only scan";overview.Metric="Scanning…";overview.Detail="Your files stay untouched.";overview.RefreshMetric();status.Text="Looking for older cache files…";cutoff=DateTime.UtcNow.AddDays(ageIndex==0?-1:ageIndex==1?-7:-30);
            try {categories=Engine.Categories();progress.Maximum=categories.Count;progress.Value=0;foreach(var c in categories){if(cancelled)break;status.Text="Scanning "+c.Name.ToLowerInvariant()+"…";await Task.Run(()=>Engine.Scan(c,cutoff,()=>cancelled));progress.Value++;}scanned=!cancelled;Populate(scanned);status.Text=cancelled?"Scan stopped. Start a new scan when you are ready.":"Scan complete. Preview the selected files, then clean when you are ready.";}
            catch(Exception ex){status.Text="Scan could not finish: "+ex.Message;}
            finally {SetBusy(false);if(scanned)UpdateAmount();else {overview.Metric="Ready to scan";overview.Caption="Scan paused";overview.Detail="Start a new scan to refresh.";overview.RefreshMetric();}}
        }
        async Task DoClean(){var selected=Selected();if(!scanned||!selected.Any(c=>c.Files.Count>0))return;using(var dialog=CreateConfirmation(selected)){if(dialog.ShowDialog(this)!=DialogResult.OK)return;}
            cancelled=false;SetBusy(true);progress.Visible=false;overview.Caption="Cleaning selected cache";overview.HasScan=false;overview.Metric="Making space…";overview.Detail="Recent and locked files are skipped.";overview.RefreshMetric();status.Text="Removing the selected older cache files…";
            try {var r=await Task.Run(()=>Engine.Clean(selected,cutoff,()=>cancelled));overview.HasScan=true;overview.Metric=Engine.Size(r.Bytes);overview.Caption=cancelled?"Removed before stopping":"Cache removed";overview.Detail=r.Deleted.ToString("N0")+" files removed";overview.Fraction=0;status.Text=(cancelled?"Stopped. ":"Finished. ")+r.Deleted.ToString("N0")+" files removed; "+r.Skipped.ToString("N0")+" skipped. Scan again to refresh.";foreach(var row in rows){row.HasScan=false;row.Check.Checked=false;row.Invalidate();}listSummary.Text="Scan again for updated results.";}
            catch(Exception ex){status.Text="Cleaning stopped: "+ex.Message;overview.HasScan=false;overview.Metric="Scan to refresh";overview.Caption="Cleaning interrupted";overview.Detail="Check the status below.";}
            finally {scanned=false;SetBusy(false);overview.RefreshMetric();}
        }
        Form Dialog(string title,Size size){return new Form {Text=title,Size=size,MinimumSize=size,StartPosition=FormStartPosition.CenterParent,BackColor=Style.Background,ForeColor=Style.Ink,Font=Style.Type(10),MinimizeBox=false,MaximizeBox=false,ShowInTaskbar=false,AutoScaleMode=AutoScaleMode.Dpi};}
        Form CreateConfirmation(List<Category> selected){var dialog=Dialog("Confirm cache cleaning",new Size(490,335));int count=selected.Sum(c=>c.Files.Count);var title=Style.Label("Ready to make some space?",19,true);title.SetBounds(24,24,435,40);dialog.Controls.Add(title);
            var summary=Style.Label(Engine.Size(selected.Sum(c=>c.Bytes))+" · "+count.ToString("N0")+(count==1?" older cache file":" older cache files"),12,true);summary.SetBounds(25,78,430,30);dialog.Controls.Add(summary);
            var note=Style.Label("These files will be permanently deleted.\nDocuments, photos, cookies and passwords stay.\nApps may rebuild their caches after cleaning.",10);note.SetBounds(25,122,430,83);dialog.Controls.Add(note);
            var stop=new SoftButton {Text="Keep files",DialogResult=DialogResult.Cancel};stop.SetBounds(135,233,140,42);var remove=new SoftButton {Text="Delete cache",Primary=true,Destructive=true,DialogResult=DialogResult.OK};remove.SetBounds(289,233,150,42);dialog.Controls.AddRange(new Control[]{stop,remove});dialog.CancelButton=stop;dialog.Shown+=(s,e)=>stop.Focus();return dialog;}
        public Form CreatePreview(){var files=Selected().SelectMany(c=>c.Files).ToList();var dialog=Dialog("Review selected cache",new Size(920,570));var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=4};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,56));dialog.Controls.Add(layout);
            var title=Style.Label("Review before you remove.",20,true);title.Dock=DockStyle.Fill;layout.Controls.Add(title);var count=Style.Label(files.Count.ToString("N0")+(files.Count==1?" selected file · ":" selected files · ")+Engine.Size(files.Sum(f=>f.Size))+" · No files are deleted by this preview.",10);count.Dock=DockStyle.Fill;layout.Controls.Add(count);
            var list=new ListView {Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,BackColor=Style.Surface,ForeColor=Style.Ink,BorderStyle=BorderStyle.None,HideSelection=false,ShowItemToolTips=true};list.Columns.Add("File path",580);list.Columns.Add("Size",90);list.Columns.Add("Modified (UTC)",150);list.BeginUpdate();foreach(var f in files.Take(10000)){var item=new ListViewItem(f.Path);item.ToolTipText=f.Path;item.SubItems.Add(Engine.Size(f.Size));item.SubItems.Add(f.Modified.ToString("yyyy-MM-dd HH:mm"));list.Items.Add(item);}list.EndUpdate();layout.Controls.Add(list);
            var bottom=new Panel {Dock=DockStyle.Fill};layout.Controls.Add(bottom);var note=Style.Label("Showing "+Math.Min(files.Count,10000).ToString("N0")+" of "+files.Count.ToString("N0")+" selected files.",9);note.SetBounds(0,17,600,26);bottom.Controls.Add(note);var close=new SoftButton {Text="Done",Primary=true,DialogResult=DialogResult.Cancel,Dock=DockStyle.Right,Width=112,Margin=new Padding(0,10,0,0)};bottom.Controls.Add(close);dialog.CancelButton=close;return dialog;}
        public void VerifyUiAndCapture(string path){if(!scanned)throw new Exception("UI scan did not complete.");bool any=Selected().Any(c=>c.Files.Count>0);if(clean.Enabled!=any||preview.Enabled!=any)throw new Exception("Action state mismatch.");
            SavePreview(this,path);using(var previewDialog=CreatePreview()){previewDialog.Show(this);Application.DoEvents();SavePreview(previewDialog,Path.ChangeExtension(path,"preview.png"));previewDialog.Close();}
            using(var confirm=CreateConfirmation(Selected())){confirm.Show(this);Application.DoEvents();SavePreview(confirm,Path.ChangeExtension(path,"confirm.png"));confirm.Close();}
            foreach(var r in rows)r.Check.Checked=false;if(clean.Enabled||preview.Enabled)throw new Exception("Empty selection was actionable.");ages[1].PerformClick();if(clean.Enabled||preview.Enabled||scanned)throw new Exception("Age change did not invalidate scan.");
            Size=new Size(960,720);Application.DoEvents();SavePreview(this,Path.ChangeExtension(path,"compact.png"));File.WriteAllText(Path.ChangeExtension(path,"ui-checks.txt"),"PASS: real read-only scan; action enablement; checkbox deselection; age change invalidates scan; full and compact layout; preview and confirmation rendering. No real files deleted.");
        }
        static void SavePreview(Form form,string path){using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(0,0,form.Width,form.Height));bmp.Save(path);}}
    }
}
