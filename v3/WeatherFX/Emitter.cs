using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace WeatherFX
{
    public abstract class Emitter : Control
    {
        public abstract void AddParticles();
        public abstract void GenerateParticles();
        public abstract void UpdateParticles();
    }
}
