using System;
using System.Collections.Generic;
using System.Text;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace PowerMateCS
{
    class Powermate
    {
        public bool isSubscribing = false;

        public string processName { get; set; }
        public int processId { get; set; }
        public GattCharacteristic readCharacteristic { get; set; }

        public Powermate()
        {
            this.processName = "";
            this.processId = -1;
        }

        public event Action<Powermate> OnPress = delegate { };
        public event Action<Powermate> OnPressLeft = delegate { };
        public event Action<Powermate> OnPressRight = delegate { };
        public event Action<Powermate> OnLeft = delegate { };
        public event Action<Powermate> OnRight = delegate { };

        public void Press()
        {
            OnPress(this);
        }

        public void Left()
        {
            OnLeft(this);
        }

        public void Right()
        {
            OnRight(this);
        }

        public void PressLeft()
        {
            OnPressLeft(this);
        }

        public void PressRight()
        {
            OnPressRight(this);
        }
    }
}
