using System;
using CommandSystem;
using EventHUD.Commands.Subcommands;

namespace EventHUD.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class EvParentCommand : ParentCommand
    {
        public EvParentCommand()
        {
            LoadGeneratedCommands();
        }

        public override string Command => "ev";
        public override string[] Aliases => new[] { "event" };
        public override string Description => "Управление ивентами";

        public override void LoadGeneratedCommands()
        {
            RegisterCommand(new PrepareCommand());
            RegisterCommand(new StartCommand());
            RegisterCommand(new StopCommand());
        }

        protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            response = "Используйте: ev prepare | start | stop";
            return false;
        }
    }
}
 