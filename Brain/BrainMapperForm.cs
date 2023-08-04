using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using Accord.Math;
using System.Globalization;
using System.Threading;

namespace BrainMapper
{
    public partial class BrainMapperForm : Form
    {
        public BrainMapperForm()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            InitializeComponent();
        }

        Brain Brain;
        private void Form1_Load(object sender, EventArgs e)
        {
            Brain = new Brain(new int[] { 2, 3, 5, 3, 1 });
            Controls.Add(Brain);
            this.ClientSize = Brain.Size;
            timer.Enabled = true;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            var r = new Random();

            Brain.Neurons.ToList().ForEach(nl => nl.ToList().ForEach(n => n.Bias = (float)r.NextDouble()));
            Brain.Synapses.ToList().ForEach(sl => sl.Flatten().ToList().ForEach(s => s.Weight = (float)r.NextDouble()));
            Brain.Refresh();
        }
    }

    public static class Exts
    {
        public static PointF Add(this Point Point, float i) => new PointF(Point.X + i, Point.Y + i);
        public static Size Add(this Size Size, int i) => new Size(Size.Width + i, Size.Height + i);
        public static Size Sub(this Size Size, int i) => Size.Add(-i);
        public static Size Multiply(this Size Size, float i) => new Size((int)(Size.Width * i), (int)(Size.Height * i));
        public static SizeF Multiply(this SizeF Size, float i) => new SizeF(Size.Width * i, Size.Height * i);
        public static Size Divide(this Size Size, float i) => Size.Multiply(1 / i);
        public static Color Negative(this Color C, bool b) { if (b) { var c = (C.R + C.G + C.B) / 3 < 128 ? 255 : 0; return Color.FromArgb(C.A, c, c, c); } else { return Color.FromArgb(C.A, 255 - C.R, 255 - C.G, 255 - C.B); } }
        public static float[] Sequance(this int Length, float Min, float Max) => Enumerable.Range(0, Length).ToList().ConvertAll(i => (Max - Min) * (((float)i) / (Length - 1)) + Min).ToArray();
        public static T GetLast<T>(this T[] Ts) => Ts[Ts.Length - 1];
        public static T GetFirst<T>(this T[] Ts) => Ts[0];
    }

    public class ColorDistributor
    {
        public Color GetColor(float Point)
        {
            int r = Line.RemoveAt(0).ToList().FindIndex(p => Point <= p);

            return Color.FromArgb(
                (int)(Slopes[0, r] * Point + Constants[0, r]),
                (int)(Slopes[1, r] * Point + Constants[1, r]),
                (int)(Slopes[2, r] * Point + Constants[2, r]),
                (int)(Slopes[3, r] * Point + Constants[3, r]));
        }

        public float[] Line;
        public float[,] Slopes;
        public float[,] Constants;

        public ColorDistributor(Color[] Colors, float Min, float Max) : this(Colors, Colors.Length.Sequance(Min, Max)) { }
        public ColorDistributor(Color[] Colors, float[] Line)
        {
            this.Line = Line;
            Slopes = new float[4, Colors.Length - 1];
            Constants = new float[4, Colors.Length - 1];

            var b = Line.GetLast() == Line.GetFirst();
            for (int r = 0; r < Line.Length - 1; r++)//regions
            {
                Slopes[0, r] = b ? 0 : (Colors[r + 1].A - Colors[r].A) / (Line[r + 1] - Line[r]);
                Slopes[1, r] = b ? 0 : (Colors[r + 1].R - Colors[r].R) / (Line[r + 1] - Line[r]);
                Slopes[2, r] = b ? 0 : (Colors[r + 1].G - Colors[r].G) / (Line[r + 1] - Line[r]);
                Slopes[3, r] = b ? 0 : (Colors[r + 1].B - Colors[r].B) / (Line[r + 1] - Line[r]);
                Constants[0, r] = b ? Colors.GetLast().A : Colors[r].A - Line[r] * Slopes[0, r];
                Constants[1, r] = b ? Colors.GetLast().R : Colors[r].R - Line[r] * Slopes[1, r];
                Constants[2, r] = b ? Colors.GetLast().G : Colors[r].G - Line[r] * Slopes[2, r];
                Constants[3, r] = b ? Colors.GetLast().B : Colors[r].B - Line[r] * Slopes[3, r];
            };
        }
    }

    public class Brain : Panel
    {
        public float[] Input { get => InputNeurons.ToList().Select(n => n.Input).ToArray(); }
        public float[] Output { get => OutputNeurons.ToList().Select(n => n.Output).ToArray(); }

        public static readonly Color NeuronColor = Color.Blue;
        public static readonly Color SynapseColor = Color.Green;
        public static readonly int LayerGap = 120;
        public static readonly int NeuronGap = 10;

        public InputNeuron[] InputNeurons;
        public Neuron[][] Neurons;
        public OutputNeuron[] OutputNeurons;
        public Synapse[][,] Synapses;

        private ToolTip ToolTip;

        public Brain() : this(new int[] { 1, 1 }) { }
        public Brain(int[] Layers)
        {
            this.Paint += Brain_Paint;
            this.MouseClick += Brain_MouseClick;
            this.BorderStyle = BorderStyle.FixedSingle;
            this.Location = new Point(0, 0);
            this.ToolTip = new ToolTip();

            var LargestLayer = Layers.Max();
            var MaxHeight = (Neuron.MSize.Height + NeuronGap) * LargestLayer - NeuronGap;
            var Center = MaxHeight / 2;
            var r = new Random();

            Neurons = new Neuron[Layers.Length][];


            for (int l = 0; l < Layers.Length; l++)
            {
                Type arraytype = l == 0 ? typeof(InputNeuron[]) : l == Layers.Length - 1 ? typeof(OutputNeuron[]) : typeof(Neuron[]);
                Type type = l == 0 ? typeof(InputNeuron) : l == Layers.Length - 1 ? typeof(OutputNeuron) : typeof(Neuron);

                Neurons[l] = (Neuron[])Activator.CreateInstance(arraytype, new object[] { Layers[l] });

                for (int n = 0; n < Neurons[l].Length; n++)
                {
                    Neurons[l][n] = (Neuron)Activator.CreateInstance(type, new object[] { new Point(LayerGap * l, (int)((Neuron.MSize.Height + NeuronGap) * (n + (LargestLayer - Layers[l]) / 2f))), Neuron.MSize, (float)r.NextDouble() });
                }
            }

            InputNeurons = (InputNeuron[])Neurons[0];
            OutputNeurons = (OutputNeuron[])Neurons[Layers.Length - 1];

            Synapses = new Synapse[Layers.Length - 1][,];
            for (int l = 0; l < Layers.Length - 1; l++)
            {
                Synapses[l] = new Synapse[Layers[l], Layers[l + 1]];
                for (int si = 0; si < Synapses[l].GetLength(0); si++)
                {
                    for (int sj = 0; sj < Synapses[l].GetLength(1); sj++)
                    {
                        Synapses[l][si, sj] = new Synapse(Neurons[l][si].Right, Neurons[l][si].Center.Y, Neurons[l + 1][sj].Left, Neurons[l + 1][sj].Center.Y, (float)r.NextDouble());
                    }
                }
            }

            this.Size = new Size(LayerGap * (Layers.Length - 1) + Neuron.MSize.Width + 100, MaxHeight).Add(3);
        }

        private void Brain_MouseClick(object sender, MouseEventArgs e)
        {
            var l = e.Location.X / LayerGap;

            string Text = "";

            if ((e.Location.X % LayerGap) < Neuron.MSize.Width)
            {
                var dists = Neurons[l].Select(Neuron => Neuron.Distance(e.Location)).ToList();
                var n = dists.IndexOf(dists.Min());
                Text =
                    $"Neuron {n + 1} at Layer {l + 1}\n" +
                    $"Its Bias is {Neurons[l][n].Bias:0.00}";
            }
            else
            {
                if (l < Synapses.Length)
                {
                    var SynapseMatrixArray = Synapses[l].Flatten();
                    var dists = SynapseMatrixArray.Select(Synapse => Synapse.Distance(e.Location)).ToList();
                    var s = dists.IndexOf(dists.Min());

                    Text =
                        $"Synapses {(l == 0 ? 0 : Synapses[l - 1].Length) + s + 1} that goes\n" +
                        $"Neuron from {s / Neurons[l + 1].Length + 1} to {s % Neurons[l + 1].Length + 1} at Layer {l + 1}\n" +
                        $"Its Weight is {SynapseMatrixArray[s].Weight:0.00}";
                }
            }

            ToolTip.Show(Text, this, e.Location, 5000);
        }

        private void Brain_Paint(object sender, PaintEventArgs e)
        {
            var NeuronsArray = Neurons.Flatten().ToList();
            var MaxNeuron = NeuronsArray.Max(n => n.Bias);
            var MinNeuron = NeuronsArray.Min(n => n.Bias);

            var SynapsesArray = new List<Synapse>();
            Synapses.ToList().ForEach(SynapseMatrix => SynapsesArray.AddRange(SynapseMatrix.Flatten()));
            var MaxSynapse = SynapsesArray.Max(s => s.Weight);
            var MinSynapse = SynapsesArray.Min(s => s.Weight);

            ColorDistributor NeuronColorDistributor = new ColorDistributor(new Color[] { Color.Black, NeuronColor, Color.White }, MinNeuron, MaxNeuron);
            ColorDistributor SynapseColorDistributor = new ColorDistributor(new Color[] { Color.Black, SynapseColor, Color.White }, MinSynapse, MaxSynapse);

            using (Graphics g = e.Graphics)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var width = 2f;

                NeuronsArray.ForEach(nn => nn.Paint(NeuronColorDistributor.GetColor(nn.Bias), g));

                SynapsesArray.ForEach(ss =>
                {
                    g.DrawLine(new Pen(new SolidBrush(SynapseColorDistributor.GetColor(ss.Weight)), 3 * width) { StartCap = LineCap.Round, EndCap = LineCap.Round }, ss.Start.X, ss.Start.Y, ss.End.X, ss.End.Y);
                    g.DrawLine(Pens.Black, ss.Start.X, ss.Start.Y, ss.End.X, ss.End.Y);
                    ss.Paint(SynapseColorDistributor.GetColor(ss.Weight), g);
                });
            }
        }

        public void Brain_Refresh()
        {
            Refresh();
            Brain_Paint(this, new PaintEventArgs(this.CreateGraphics(), ClientRectangle));
        }
    }

    public class Neuron
    {
        public static readonly Size MSize = new Size(50, 50);
        public static float Sigmoid(float Input) => 1f / (1 + (float)Math.Pow(Math.E, -Input));

        public float Bias { get => bias; set { bias = value; BiasChanged?.Invoke(this, EventArgs.Empty); } }
        float bias;
        public event Action<object, EventArgs> BiasChanged;

        public Point Location;
        public Size Size;
        public int Top => Location.Y;
        public int Bottom => Location.Y + Size.Height;
        public int Left => Location.X;
        public int Right => Location.X + Size.Width;

        public Point Center => Point.Add(Location, Size.Divide(2f));
        public float Distance(Point Point) => (float)Math.Sqrt(Math.Pow(Center.X - Point.X, 2) + Math.Pow(Center.Y - Point.Y, 2));

        public Neuron(Point Location, Size Size, float Bias)
        {
            this.Location = Location;
            this.Size = Size;
            this.Bias = Bias;
        }

        public virtual void Paint(Color c, Graphics g)
        {
            var width = 3;
            g.FillEllipse(new SolidBrush(c), new RectangleF(Location, Size));
            g.DrawEllipse(new Pen(Brushes.Black, width), new RectangleF(Location.Add(0.5f * width), Size.Sub(width)));

            var fontsize = 10;
            Font f = new Font(FontFamily.GenericMonospace, fontsize, FontStyle.Bold);
            SizeF textSize = g.MeasureString(Bias.ToString("0.00"), f);
            g.DrawString(Bias.ToString("0.00"), f, new SolidBrush(c.Negative(true)), Center.X - textSize.Width / 2, Center.Y - textSize.Height / 2);
        }
    }

    public class InputNeuron : Neuron
    {
        private new float Bias { get => base.Bias; set => base.Bias = value; }
        public float Input { get => Bias; set => Bias = value; }

        private new event Action<object, EventArgs> BiasChanged { add { base.BiasChanged += value; } remove { base.BiasChanged -= value; } }
        public event Action<object, EventArgs> InputChanged { add { BiasChanged += value; } remove { BiasChanged -= value; } }

        public InputNeuron(Point Location, Size Size, float Input) : base(Location, Size, Input)
        {

        }

        public override void Paint(Color c, Graphics g)
        {
            var width = 3;
            g.FillRectangle(new SolidBrush(c), new RectangleF(Location, Size));
            var loc = Location.Add(0.5f * width);
            var size = Size.Sub(width);
            g.DrawRectangle(new Pen(Brushes.Black, width), loc.X, loc.Y, size.Width, size.Height);

            var fontsize = 10;
            Font f = new Font(FontFamily.GenericMonospace, fontsize, FontStyle.Bold);
            SizeF textSize = g.MeasureString(Bias.ToString("0.00"), f);
            g.DrawString(Bias.ToString("0.00"), f, new SolidBrush(c.Negative(true)), Center.X - textSize.Width / 2, Center.Y - textSize.Height / 2);
        }
    }

    public class OutputNeuron : Neuron
    {
        public float Output { get => output; set { output = value; OutputChanged?.Invoke(this, EventArgs.Empty); } }
        private float output;

        public event Action<object, EventArgs> OutputChanged;

        public OutputNeuron(Point Location, Size Size, float Input) : base(Location, Size, Input)
        {

        }

        public override void Paint(Color c, Graphics g)
        {
            base.Paint(c, g);
            var width = 3;
            var length = 10;
            g.DrawLine(new Pen(Brushes.Black, width), Right, Center.Y, Right + length, Center.Y);

            var fontsize = 10;
            Font f = new Font(FontFamily.GenericMonospace, fontsize, FontStyle.Bold);
            SizeF textSize = g.MeasureString(Output.ToString("0.00"), f);
            var recsize = textSize.Multiply(1.2f);
            var recloc = new PointF(Right + length, Center.Y - recsize.Height / 2);
            g.FillRectangle(Brushes.White, new RectangleF(recloc, recsize));
            g.DrawRectangle(new Pen(Brushes.Black, width), recloc.X, recloc.Y, recsize.Width, recsize.Height);
            g.DrawString(Output.ToString("0.00"), f, Brushes.Black, recloc.X + (recsize.Width - textSize.Width) / 2, recloc.Y + (recsize.Height - textSize.Height) / 2);
        }
    }

    public class Synapse
    {
        public float Weight { get => weight; set { weight = value; WeightChanged?.Invoke(this, EventArgs.Empty); } }
        float weight;
        public event Action<object, EventArgs> WeightChanged;

        public Point Start;
        public Point End;
        public Point Center => new Point((Start.X + End.X) / 2, (Start.Y + End.Y) / 2);
        float Slope => ((float)(End.Y - Start.Y)) / (End.X - Start.X);
        float Constant => Start.X * Slope - Start.Y;
        public float Distance(Point Point) => (float)(Math.Abs(Point.Y - Slope * Point.X + Constant) / Math.Sqrt(Math.Pow(Slope, 2) + 1));
        public Synapse(int StartX, int StartY, int EndX, int EndY, float Weight) : this(new Point(StartX, StartY), new Point(EndX, EndY), Weight) { }

        public Synapse(Point Start, Point End, float Weight)
        {
            this.Start = Start;
            this.End = End;
            this.Weight = Weight;
        }

        public void Paint(Color c, Graphics g)
        {
            var width = 2;
            g.DrawLine(new Pen(new SolidBrush(c), 3 * width) { StartCap = LineCap.Round, EndCap = LineCap.Round }, Start.X, Start.Y, End.X, End.Y);
            g.DrawLine(Pens.Black, Start.X, Start.Y, End.X, End.Y);
        }
    }
}