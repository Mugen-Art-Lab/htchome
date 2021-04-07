using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Weather.Base
{
    //костыль для добавления новых фич в MSN, но с сохранением обратной совместимости со старыми провайдерами
    public static class Shared
    {
        public static TemperatureScale TemperatureScale;
    }
}
