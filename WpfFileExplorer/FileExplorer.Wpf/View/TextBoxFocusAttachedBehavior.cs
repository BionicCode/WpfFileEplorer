using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JetBrains.Annotations;

namespace FileExplorer.Wpf.View
{
  /// <summary>
  /// Implementation of attached behavior.<para/>
  /// Class controls the behavior of any TextBox attached to. <para/>
  /// The Textbox must be part of a ListBoxItem DataTemplate. <para/>
  /// Gives the TextBox focus on item selected and performs actions like moving caret to end  of  line or preselecting content.<para/>
  /// </summary>
  /// <remarks>
  /// Listen to the parent ListBoxItem's PreviewMouseLeftButtonUp to handle focus behavior. <para/>
  /// Using the Selected event or in general the default ListBox selection mechanism  <para/>
  /// would interfere with other user input situations (e.g. opening context menu, which involves  <para/>
  /// the corresponding item to be selected. So stealing the focus in such case leads to odd behavior). 
  /// </remarks>
  internal class TextBoxFocusAttachedBehavior
  {

    #region SetCaretToTextEnd attached property

    public static readonly DependencyProperty SetCaretToTextEndProperty = DependencyProperty.RegisterAttached(
      "SetCaretToTextEnd", typeof(bool), typeof(TextBoxFocusAttachedBehavior), new PropertyMetadata(false, TextBoxFocusAttachedBehavior.OnSetCaretToEndEnabledChanged));


    public static void SetSetCaretToTextEnd([NotNull] DependencyObject attachingElement, bool value)
    {
      attachingElement.SetValue(TextBoxFocusAttachedBehavior.SetCaretToTextEndProperty, value);
    }

    public static bool GetSetCaretToTextEnd([NotNull] DependencyObject attachingElement)
    {
      return (bool) attachingElement.GetValue(TextBoxFocusAttachedBehavior.SetCaretToTextEndProperty);
    }

    #endregion

    #region AutoSelectText attached property

    private static readonly DependencyProperty AutoSelectTextProperty = DependencyProperty.RegisterAttached(
      "AutoSelectText", typeof(bool), typeof(TextBoxFocusAttachedBehavior), new PropertyMetadata(false, TextBoxFocusAttachedBehavior.OnAutoSelectTextChanged));


    public static void SetAutoSelectText([NotNull] DependencyObject attachingElement, bool value)
    {
      attachingElement.SetValue(TextBoxFocusAttachedBehavior.AutoSelectTextProperty, value);
    }

    public static bool GetAutoSelectText([NotNull] DependencyObject attachingElement)
    {
      return (bool) attachingElement.GetValue(TextBoxFocusAttachedBehavior.AutoSelectTextProperty);
    }

    #endregion

    #region AttachedElementIsRegistered attached property

    private static readonly DependencyProperty AttachedElementIsRegisteredProperty = DependencyProperty.RegisterAttached(
      "AttachedElementIsRegistered", typeof(bool), typeof(TextBoxFocusAttachedBehavior), new PropertyMetadata(default(bool)));

    private static void SetAttachedElementIsRegistered([NotNull] DependencyObject attachingElement, bool value)
    {
      attachingElement.SetValue(TextBoxFocusAttachedBehavior.AttachedElementIsRegisteredProperty, value);
    }

    private static bool GetAttachedElementIsRegistered([NotNull] DependencyObject attachingElement)
    {
      return (bool) attachingElement.GetValue(TextBoxFocusAttachedBehavior.AttachedElementIsRegisteredProperty);
    }

    #endregion

    #region ParentItemTextBox attached property

    private static readonly DependencyProperty ParentItemTextBoxProperty = DependencyProperty.RegisterAttached(
      "ParentItemTextBox", typeof(TextBox), typeof(TextBoxFocusAttachedBehavior), new PropertyMetadata(default(TextBox)));

    private static void SetParentItemTextBox([NotNull] DependencyObject attachingElement, TextBox value)
    {
      attachingElement.SetValue(TextBoxFocusAttachedBehavior.ParentItemTextBoxProperty, value);
    }

    private static TextBox GetParentItemTextBox([NotNull] DependencyObject attachingElement)
    {
      return (TextBox) attachingElement?.GetValue(TextBoxFocusAttachedBehavior.ParentItemTextBoxProperty);
    }

    #endregion

    private static void OnAutoSelectTextChanged(DependencyObject attachingElement, DependencyPropertyChangedEventArgs e) => TextBoxFocusAttachedBehavior.ObserveAttachedElement(attachingElement, (bool) e.NewValue);
    private static void OnSetCaretToEndEnabledChanged(DependencyObject attachingElement, DependencyPropertyChangedEventArgs e) => TextBoxFocusAttachedBehavior.ObserveAttachedElement(attachingElement, (bool)e.NewValue);

    /// <summary>
    /// Listen to the parent ListBoxItem's PreviewMouseLeftButtonUp to handle focus behavior. <para/>
    /// Using the Selected event or in general the default ListBox selection mechanism  <para/>
    /// would interfere with other user input (e.g. opening context menu, which involves  <para/>
    /// the corresponding item to be selected. So stealing the focus in such case leads to odd behavior.)
    /// </summary>
    /// <param name="attachingElement"></param>
    /// <param name="isEnabled"></param>
    private static void ObserveAttachedElement(DependencyObject attachingElement, bool isEnabled)
    {
      ListBox itemsControl;
      if (TextBoxFocusAttachedBehavior.TryFindAttachedElementsParentItemsControl(attachingElement, out itemsControl))
      {
        ListBoxItem item;
        if (TextBoxFocusAttachedBehavior.TryFindAttachedElementsParentItem(attachingElement, out item))
        {
          if (isEnabled)
          {
            // Ensure to listen to event with only one handler assigned each ItemsControl
            if (!TextBoxFocusAttachedBehavior.GetAttachedElementIsRegistered(itemsControl))
            {
              //LogDocumentKeyboardInputHandler.Navigated += TextBoxFocusAttachedBehavior.HandleFocusOnKeyboardNavigated;
            }

            // Store the item's attached TextBox for later use with the event handlers
            TextBoxFocusAttachedBehavior.SetParentItemTextBox(item, attachingElement as TextBox);


            WeakEventManager<UIElement, MouseButtonEventArgs>.AddHandler(
              item, 
              "PreviewMouseLeftButtonUp",
              TextBoxFocusAttachedBehavior.HandleItemLeftMouseUp);

            TextBoxFocusAttachedBehavior.SetAttachedElementIsRegistered(item, true);
          }
          else
          {
            //LogDocumentKeyboardInputHandler.Navigated -= TextBoxFocusAttachedBehavior.HandleFocusOnKeyboardNavigated;

            // Release the attached TextBox
            TextBoxFocusAttachedBehavior.SetParentItemTextBox(item, null);

            WeakEventManager<UIElement, MouseButtonEventArgs>.RemoveHandler(
              item,
              "PreviewMouseLeftButtonUp",
              TextBoxFocusAttachedBehavior.HandleItemLeftMouseUp);

            TextBoxFocusAttachedBehavior.SetAttachedElementIsRegistered(item, false);
          }
        }
      }
    }

    private static void HandleFocusOnKeyboardNavigated(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
    {
      if (dependencyObject is ItemsControl)
      {
        var itemsControl = dependencyObject as ItemsControl;
        TextBox parentItemTextBox = TextBoxFocusAttachedBehavior.GetParentItemTextBox(itemsControl.ItemContainerGenerator.ContainerFromItem(dependencyPropertyChangedEventArgs.NewValue));
        if (parentItemTextBox == null)
        {
          return;
        }

        //TextBoxFocusAttachedBehavior.ApplyBehaviors(parentItemTextBox);
      }
    }

    private static void HandleItemLeftMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
      eventArgs.Handled = false;
      if (eventArgs.OriginalSource is TextBox)
      {
        ListBoxItem textBoxParentItem;
        ListBox textBoxParentItemsControl;
        if (TextBoxFocusAttachedBehavior.TryFindAttachedElementsParentItem(
              eventArgs.OriginalSource as DependencyObject,
              out textBoxParentItem)
            && TextBoxFocusAttachedBehavior.TryFindAttachedElementsParentItemsControl(
              eventArgs.OriginalSource as DependencyObject,
              out textBoxParentItemsControl))
        {
          textBoxParentItemsControl.SelectedItem = textBoxParentItem.Content;
        }

        return;
      }

      // Apply behavior when ListBoxItem is clicked anywhere else but Buttons, Checkboxes, etc
      if (eventArgs.OriginalSource is Border)
      {
        var parentItem = sender as ListBoxItem;
        TextBox textBox = TextBoxFocusAttachedBehavior.GetParentItemTextBox(parentItem);

        TextBoxFocusAttachedBehavior.ApplyBehaviors(textBox);
      }
    }

    private static void ApplyBehaviors([NotNull] TextBox textBox)
    {
      if (textBox.IsFocused)
      {
        return;
      }

      Keyboard.Focus(textBox);
      if (TextBoxFocusAttachedBehavior.GetSetCaretToTextEnd(textBox))
      {
        TextBoxFocusAttachedBehavior.MoveAttachedTextBoxCaretToEnd(textBox);
      }

      if (TextBoxFocusAttachedBehavior.GetAutoSelectText(textBox))
      {
        TextBoxFocusAttachedBehavior.SelectAttachedTextBoxText(textBox);
      }
    }

    private static bool TryFindAttachedElementsParentItem(DependencyObject attachingElement, out ListBoxItem parentListBoxItem)
    {
      parentListBoxItem = new ListBoxItem();
      ListBox itemsControl;
      if (TextBoxFocusAttachedBehavior.TryFindAttachedElementsParentItemsControl(attachingElement, out itemsControl))
      {
         var parentItem = itemsControl.ItemContainerGenerator.ContainerFromItem(
          (attachingElement as FrameworkElement)?.DataContext) as ListBoxItem;

        parentListBoxItem = parentItem ?? new ListBoxItem();
        return parentItem != null;
      }

      return false;
    }

    private static bool TryFindAttachedElementsParentItemsControl(DependencyObject attachingElement, out ListBox parentListBox)
    {
      var dependencyObject = attachingElement;
      while (dependencyObject != null && !(dependencyObject is ListBox))
      {
        dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
      }

      parentListBox = dependencyObject as ListBox ;
      return parentListBox != null;
    }


    private static void SelectAttachedTextBoxText(TextBox textBox) => textBox.SelectAll();
    private static void MoveAttachedTextBoxCaretToEnd(TextBox textBox) => textBox.CaretIndex = textBox.Text.Length;
  }
}
