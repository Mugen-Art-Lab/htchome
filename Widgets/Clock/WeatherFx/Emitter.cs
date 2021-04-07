using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Clock.WeatherFx
{
    public class Emitter
    {
        public Queue<Particle> Particles { get; private set; }
        public int MaxParticles { get; set; }
        public Point Position { get; set; }
        public Vector Velocity { get; set; }
        public double LifeTime { get; set; } //particles life time
        public Vector Wind { get; set; }
        public Canvas Host { get; private set; } //canvas where particles should be drawn
        private DispatcherTimer timer;

        protected virtual void ActivateParticle(Particle p)
        {
            p.Control = new Ellipse();
            p.Control.Fill = Brushes.White;
            p.Position = Position;
            p.Velocity = Velocity;
            p.Size = new Size(20, 20);
            p.Brush = Brushes.White;
            p.LifeTime = LifeTime;
            p.IsAlive = true;
        }

        public virtual void Initialize(ref Canvas host)
        {
            Host = host;
            Particles = new Queue<Particle>(MaxParticles);
            for (var i = 0; i < MaxParticles; i++)
            {
                var p = new Particle();
                ActivateParticle(p);
                Particles.Enqueue(p);
                Host.Children.Add(p.Control);
            }

            //using timer instead of CompositionTarget.Rendering event because it provides stable FPS and less CPU load
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(10);
            timer.Tick += new EventHandler(TimerTick);
            timer.Start();
            //CompositionTarget.Rendering += new EventHandler(CompositionTargetRendering);
        }

        void TimerTick(object sender, EventArgs e)
        {
            Update();
        }


        protected virtual void Update()
        {
            foreach (var particle in Particles)
            {
                if (particle.IsAlive)
                    particle.Update();
                else
                {
                    particle.Position = Position;
                    particle.IsAlive = true;
                }
            }
        }
    }
}
