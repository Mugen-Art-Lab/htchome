using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Clock.WeatherFx
{
    public class Particle
    {
        private double creationTime;
        private bool isAlive;

        public Shape Control { get; set; }

        public Brush Brush
        {
            get { return Control.Fill; }
            set { Control.Fill = value; }
        }
        public bool IsAlive
        {
            get { return isAlive; }
            set
            {
                if (value) //when particle goes alive 
                {
                    creationTime = Environment.TickCount; //it is a creation moment
                    //Control.Opacity = 0;
                }
                isAlive = value;
            }
        }

        public Vector Velocity { get; set; }
        public Point Position { get; set; }
        public Size Size
        {
            get {return new Size(Control.Width, Control.Height);}
            set
            {
                Control.Width = value.Width;
                Control.Height = value.Height;
            }
        }

        public double Age //particle age in seconds
        {
            get { return (Environment.TickCount - creationTime) / 1000.0f; }
        }

        public double LifeTime { get; set; } //particle life time in seconds

        public void Update()
        {
           
            /*if (Age >= LifeTime)
                IsAlive = false;*/
            //else
            //{
                var age = Age;
                //if (1 - (age / LifeTime) <= 0.1)
                //{

                //    Control.Opacity = (1 - (age / LifeTime)) * 10;
                //}

                /*if (age / LifeTime <= 0.1)
                {

                    Control.Opacity = (age / LifeTime) * 10;
                }*/

                Position += Velocity;
                Canvas.SetLeft(Control, Position.X);
                Canvas.SetTop(Control, Position.Y);
            //}
        }
    }
}
