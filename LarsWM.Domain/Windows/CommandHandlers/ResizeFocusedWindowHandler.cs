﻿using System.Linq;
using LarsWM.Domain.Common.Enums;
using LarsWM.Domain.Containers;
using LarsWM.Domain.Containers.Commands;
using LarsWM.Domain.UserConfigs;
using LarsWM.Domain.Windows.Commands;
using LarsWM.Infrastructure.Bussing;

namespace LarsWM.Domain.Windows.CommandHandlers
{
  class ResizeFocusedWindowHandler : ICommandHandler<ResizeFocusedWindowCommand>
  {
    private Bus _bus;
    private WindowService _windowService;
    private UserConfigService _userConfigService;
    private ContainerService _containerService;

    public ResizeFocusedWindowHandler(Bus bus, WindowService windowService, UserConfigService userConfigService, ContainerService containerService)
    {
      _bus = bus;
      _windowService = windowService;
      _userConfigService = userConfigService;
      _containerService = containerService;
    }

    public dynamic Handle(ResizeFocusedWindowCommand command)
    {
      var focusedWindow = _containerService.FocusedContainer as Window;

      // Ignore cases where focused container is not a window.
      if (focusedWindow == null)
        return CommandResponse.Ok;

      var siblings = focusedWindow.Siblings;

      // Ignore cases where focused window doesn't have any siblings.
      if (siblings.Count() == 0)
        return CommandResponse.Ok;

      var parent = focusedWindow.Parent as SplitContainer;
      var layout = parent.Layout;
      var resizePercentage = _userConfigService.UserConfig.ResizePercentage;
      var resizeDirection = command.ResizeDirection;

      // TODO: Handle error where a horizontal workspace has 2 windows, and user attempts to grow the height of
      // one of the containers.

      if (
        layout == Layout.Horizontal && resizeDirection == ResizeDirection.GROW_WIDTH
        || layout == Layout.Vertical && resizeDirection == ResizeDirection.GROW_HEIGHT
      )
      {
        focusedWindow.SizePercentage += resizePercentage;

        foreach (var sibling in siblings)
          sibling.SizePercentage -= resizePercentage / siblings.Count();

        _containerService.SplitContainersToRedraw.Add(parent);
      }

      if (
        layout == Layout.Vertical && resizeDirection == ResizeDirection.GROW_WIDTH
        || layout == Layout.Horizontal && resizeDirection == ResizeDirection.GROW_HEIGHT
      )
      {
        parent.SizePercentage += resizePercentage;

        foreach (var sibling in parent.Siblings)
          sibling.SizePercentage -= resizePercentage / siblings.Count();

        _containerService.SplitContainersToRedraw.Add(parent.Parent as SplitContainer);
      }

      if (
        layout == Layout.Horizontal && resizeDirection == ResizeDirection.SHRINK_WIDTH
        || layout == Layout.Vertical && resizeDirection == ResizeDirection.SHRINK_HEIGHT
      )
      {
        focusedWindow.SizePercentage -= resizePercentage;

        foreach (var sibling in siblings)
          sibling.SizePercentage += resizePercentage / siblings.Count();

        _containerService.SplitContainersToRedraw.Add(parent);
      }

      if (
        layout == Layout.Vertical && resizeDirection == ResizeDirection.SHRINK_WIDTH
        || layout == Layout.Horizontal && resizeDirection == ResizeDirection.SHRINK_HEIGHT
      )
      {
        parent.SizePercentage -= resizePercentage;

        foreach (var sibling in parent.Siblings)
          sibling.SizePercentage += resizePercentage / siblings.Count();

        _containerService.SplitContainersToRedraw.Add(parent.Parent as SplitContainer);
      }

      _bus.Invoke(new RedrawContainersCommand());

      return new CommandResponse(true, focusedWindow.Id);
    }
  }
}
