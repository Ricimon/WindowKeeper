using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Ricimon.WindowKeeper.WpfShell.Util
{
    public class SelectorExtenders : DependencyObject
    {
        public static bool GetIsAutoScroll(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsAutoscrollProperty);
        }

        public static void SetIsAutoscroll(DependencyObject obj, bool value)
        {
            obj.SetValue(IsAutoscrollProperty, value);
        }

        public static readonly DependencyProperty IsAutoscrollProperty =
            DependencyProperty.RegisterAttached("IsAutoscroll", typeof(bool), typeof(SelectorExtenders), new UIPropertyMetadata(default(bool), OnIsAutoscrollChanged));

        public static void OnIsAutoscrollChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
        {
            var val = (bool)e.NewValue;
            var lb = s as ListBox;
            var ic = lb.Items;
            var data = ic.SourceCollection as INotifyCollectionChanged;

            var autoscroller = new NotifyCollectionChangedEventHandler(
                (s1, e1) =>
                {
                    object selectedItem = default(object);
                    switch (e1.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                        case NotifyCollectionChangedAction.Move:
                            selectedItem = e1.NewItems[e1.NewItems.Count - 1];
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            if (ic.Count < e1.OldStartingIndex)
                            {
                                selectedItem = ic[e1.OldStartingIndex - 1];
                            }
                            else if (ic.Count > 0)
                            {
                                selectedItem = ic[0];
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            if (ic.Count > 0)
                            {
                                selectedItem = ic[0];
                            }
                            break;
                    }

                    if (selectedItem != default(object))
                    {
                        ic.MoveCurrentTo(selectedItem);
                        lb.ScrollIntoView(selectedItem);
                    }
                });

            if (val)
            {
                data.CollectionChanged += autoscroller;
            }
            else
            {
                data.CollectionChanged -= autoscroller;
            }
        }
    }
}
