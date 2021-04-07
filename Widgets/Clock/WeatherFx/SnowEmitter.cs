using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Clock.WeatherFx
{
    public class SnowEmitter : Emitter
    {
        private Random r = new Random(Environment.TickCount);
        public double Width { get; set; }
        public double Height { get; set; }

        protected override void ActivateParticle(Particle p)
        {
            p.Control = new Ellipse();
            /*var brush = new ImageBrush();
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(string.Format("pack://application:,,,/Clock;component/Resources/FlipClock/Weather/snowflake{0}.png", r.Next(1, 6)));
            bi.EndInit();
            brush.ImageSource = bi;*/
            p.Brush = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            //p.Brush = brush;
            p.Position = new Point(r.Next((int)Position.X, (int)(Position.X + Width)), Position.Y);
            var scale = (50 + r.NextDouble()*75)/6;
            p.Size = new Size(scale, scale);
            p.Velocity = new Vector(0, (r.NextDouble() + 0.1) * (scale / 9));
            p.LifeTime = LifeTime;
            p.IsAlive = true;
           
        }

        private double wind = 0;
        private double age = 0;
        protected override void Update()
        {
            foreach (var particle in Particles)
            {
                if (particle.IsAlive)
                {
                    age += 0.001;
                    particle.Velocity = new Vector(Wind.X * particle.Size.Width / 1000, particle.Velocity.Y);

                    if (age > 0.1)
                    {
                        age = 0;
                        var newX = r.NextDouble() - r.NextDouble();
                        var chX = (newX - wind)/(50 + r.Next(50));
                        wind += chX;
                    }
                    particle.Update();
                    if (1 - (particle.Position.Y / Height) <= 0.2)
                    {

                        //particle.Control.Opacity = (1 - (particle.Position.Y / (Height * 0.5 - particle.Size.Height)));
                        particle.Control.Opacity -= 0.07;
                        if (particle.Control.Opacity <= 0)
                        {
                            particle.IsAlive = false;
                            particle.Position = new Point(0,0);
                        }
                    }
                    if (1 - (particle.Position.Y / Height) >= 0.7)
                    {
                        particle.Control.Opacity = particle.Position.Y / Height;
                    }
                }
                else
                {
                    particle.Control.Opacity = 0;
                    /*var brush = new ImageBrush();
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(string.Format("pack://application:,,,/Clock;component/Resources/FlipClock/Weather/snowflake{0}.png", r.Next(1, 6)));
                    bi.EndInit();
                    brush.ImageSource = bi;
                    particle.Brush = brush;*/
                    particle.Brush = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                    particle.Position = new Point(r.Next((int)Position.X, (int)(Position.X + Width)) - particle.Size.Width, Position.Y);
                    var scale = (50 + r.NextDouble() * 75) / 6;
                    particle.Size = new Size(scale, scale);
                    particle.Velocity = new Vector(0, (r.NextDouble() + 0.1) * (scale / 9));
                    particle.LifeTime = LifeTime;
                    particle.IsAlive = true;
                }
            }
        }
    }
}
