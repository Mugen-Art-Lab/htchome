using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Clock.WeatherAnimationNew
{
    public class RainDrop
    {
        public Ellipse ParticleControl;
        private int seed;
        private int beginTime;
        private Random r;
        private Storyboard moveAnimStoryboard;
        private DoubleAnimation moveAnim;
        private Storyboard opacityAnimStoryboard;
        private DoubleAnimation opacityAnim;
        private double speedX = 1.0;
        private double wind;
        private double lifeTime; //used for changing wind and gravity
        private double scale = 1;
        private int count;
        private int globalWindSpeed;

        public void Initialize(int seed, int begintime, int windSpeed)
        {
            this.seed = seed;
            this.beginTime = begintime;
            this.globalWindSpeed = windSpeed;

            r = new Random(Environment.TickCount * seed);
            ParticleControl = new Ellipse();
            ParticleControl.Fill = Brushes.Green;
            ParticleControl.Opacity = 0.5;
            ParticleControl.Loaded += ParticleControlLoaded;

            moveAnimStoryboard = new Storyboard();
            moveAnim = new DoubleAnimation();
            moveAnim.From = 0;
            moveAnim.To = 270;
            moveAnim.FillBehavior = FillBehavior.Stop;
            Storyboard.SetTargetProperty(moveAnim, new PropertyPath("(Canvas.Top)"));
            moveAnimStoryboard.Children.Add(moveAnim);

            opacityAnimStoryboard = new Storyboard();
            opacityAnim = new DoubleAnimation();
            opacityAnim.From = 1;
            opacityAnim.To = 0;
            opacityAnim.Duration = TimeSpan.FromMilliseconds(500);
            opacityAnim.FillBehavior = FillBehavior.Stop;
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            opacityAnimStoryboard.Children.Add(opacityAnim);
            opacityAnimStoryboard.Completed += OpacityAnimStoryboardCompleted;

            moveAnimStoryboard.CurrentTimeInvalidated += MoveAnimStoryboardCurrentTimeInvalidated;
            moveAnimStoryboard.Completed += MoveAnimStoryboardCompleted;

            if (globalWindSpeed > 1)
            {
                var rotation = new RotateTransform(globalWindSpeed / -2.0);
                ParticleControl.RenderTransformOrigin = new Point(0.5,0.5);
                ParticleControl.RenderTransform = rotation;
            }

            Setup();
        }

        void OpacityAnimStoryboardCompleted(object sender, EventArgs e)
        {
            ParticleControl.Opacity = 0;
        }

        private void Setup()
        {
            r = new Random(Environment.TickCount * seed);
            Canvas.SetTop(ParticleControl, 0);
            moveAnim.Duration = TimeSpan.FromMilliseconds(r.Next(500, 800));
            moveAnimStoryboard.BeginTime = TimeSpan.FromMilliseconds(beginTime);
            Canvas.SetLeft(ParticleControl, r.Next(100, 400));
            speedX = r.NextDouble();
            /*if (globalWindSpeed > 1)
                moveAnim.Duration -= TimeSpan.FromMilliseconds(globalWindSpeed * 100);*/
            scale = (25 + r.NextDouble() * 75) / 70;
            ParticleControl.Width = 4.4 * scale;
            ParticleControl.Height = 15 * scale;


            var brush = new ImageBrush();
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri("pack://application:,,,/UIFramework.Weather;Component/Images/rain_fall.png");
            bi.EndInit();
            brush.ImageSource = bi;
            ParticleControl.Fill = brush;
        }

        void MoveAnimStoryboardCompleted(object sender, EventArgs e)
        {
            count++;
            if (count < 25)
            {
                seed++;
                Setup();
                moveAnimStoryboard.Begin(ParticleControl);
            }
            else
                if (ParticleControl.Parent != null)
                {
                    ((Canvas)ParticleControl.Parent).Children.Remove(ParticleControl);
                }
        }

        void ParticleControlLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            moveAnimStoryboard.Begin(ParticleControl);
        }

        void MoveAnimStoryboardCurrentTimeInvalidated(object sender, EventArgs e)
        {
            MoveSnowflake();

            var c = (System.Windows.Media.Animation.Clock)sender;
            if (c == null)
                return;
            /*if (c.CurrentProgress != null && c.CurrentProgress.Value >= 0.9 && !fadeRunning)
            {
                opacityAnimStoryboard.Begin(ParticleControl);
                fadeRunning = true;
            }*/
        }

        private void MoveSnowflake()
        {
            lifeTime += 0.001;
            var cX = Canvas.GetLeft(ParticleControl);
            if (globalWindSpeed <= 1)
                cX += speedX/10 + wind * scale;
            else
            {
                cX += globalWindSpeed / 30.0;
            }
           /* if (((cX < 70 && speedX < 0) || (cX > 470 && speedX > 0)) && !fadeRunning)
            {
                opacityAnim.Duration = TimeSpan.FromMilliseconds(200);
                opacityAnimStoryboard.Begin(ParticleControl);
                fadeRunning = true;
            }*/
            Canvas.SetLeft(ParticleControl, cX);

            if (lifeTime > 0.55)
            {
                lifeTime = 0;

                var newX = r.NextDouble() - r.NextDouble();
                var chX = (newX - wind) / (50 + r.Next(50));
                wind += chX;
            }
        }
    }
}
