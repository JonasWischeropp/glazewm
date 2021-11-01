﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace GlazeWM.Domain.Containers
{
  public class Container
  {
    public virtual int Height { get; set; }
    public virtual int Width { get; set; }
    public virtual int X { get; set; }
    public virtual int Y { get; set; }
    public Container Parent { get; set; } = null;
    public List<Container> Children { get; set; } = new List<Container>();

    /// <summary>
    /// The order of which child containers last had focus.
    /// </summary>
    public List<Container> ChildFocusOrder { get; set; } = new List<Container>();

    /// <summary>
    /// The child container that last had focus.
    /// </summary>
    public Container LastFocusedChild => ChildFocusOrder.ElementAtOrDefault(0);

    /// <summary>
    /// Index of this container in parent's child focus order.
    /// </summary>
    public int FocusIndex => Parent.ChildFocusOrder.IndexOf(this);

    public List<Container> SelfAndSiblings => Parent.Children;

    public IEnumerable<Container> Siblings => Parent.Children.Where(children => children != this);

    /// <summary>
    /// Index of this container amongst its siblings.
    /// </summary>
    public int Index => Parent.Children.IndexOf(this);

    /// <summary>
    /// Get the last focused descendant by traversing downwards.
    /// </summary>
    public Container LastFocusedDescendant
    {
      get
      {
        var lastFocusedDescendant = LastFocusedChild;

        while (lastFocusedDescendant?.LastFocusedChild != null)
          lastFocusedDescendant = lastFocusedDescendant.LastFocusedChild;

        return lastFocusedDescendant;
      }
    }

    /// <summary>
    /// The sibling at the next index to this container.
    /// </summary>
    public Container NextSibling => SelfAndSiblings.ElementAtOrDefault(Index + 1);

    /// <summary>
    /// The sibling at the previous index to this container.
    /// </summary>
    public Container PreviousSibling => SelfAndSiblings.ElementAtOrDefault(Index - 1);

    // TODO: Rename to SelfAndDescendants and change to getter.
    public IEnumerable<Container> Flatten()
    {
      return new[] { this }.Concat(Children.SelectMany(children => children.Flatten()));
    }

    public IEnumerable<Container> SelfAndAncestors => new[] { this }.Concat(Ancestors);

    public IEnumerable<Container> Ancestors
    {
      get
      {
        var parent = Parent;

        while (parent != null)
        {
          yield return parent;
          parent = parent.Parent;
        }
      }
    }

    public bool HasChildren() => Children.Count > 0;

    public bool HasSiblings() => Siblings.Count() > 0;

    public IEnumerable<Container> SelfAndSiblingsOfType(Type type)
    {
      return SelfAndSiblings.Where(container => type.IsAssignableFrom(container.GetType()));
    }

    public Container GetNextSiblingOfType(Type type)
    {
      return SelfAndSiblings
        .Skip(Index + 1)
        .FirstOrDefault(container => type.IsAssignableFrom(container.GetType()));
    }

    public Container GetPreviousSiblingOfType(Type type)
    {
      return SelfAndSiblings
        .Take(Index)
        .Reverse()
        .FirstOrDefault(container => type.IsAssignableFrom(container.GetType()));
    }

    /// <summary>
    /// Get the last focused descendant that matches the given type.
    /// </summary>
    public Container LastFocusedDescendantOfType(Type type)
    {
      var stack = new Stack<Container>();
      stack.Push(this);

      // Do a depth-first search using child focus order.
      while (stack.Any())
      {
        var current = stack.Pop();

        var isMatch = type.IsAssignableFrom(current.GetType()) && !current.HasChildren();

        if (isMatch)
          return current;

        // Reverse the child focus order so that the first element is pushed last/popped first.
        foreach (var focusChild in current.ChildFocusOrder.AsEnumerable().Reverse())
          stack.Push(focusChild);
      }

      return null;
    }
  }
}