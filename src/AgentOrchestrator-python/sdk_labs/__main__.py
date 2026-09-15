import argparse
import asyncio

from sdk_labs import events_sample, mcp_sample, sessions_sample, tools_sample


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run GitHub Copilot SDK lab samples")
    subparsers = parser.add_subparsers(dest="lab", required=True)
    for name in ("tools", "events", "mcp"):
        command = subparsers.add_parser(name)
        command.add_argument("--model")
    sessions = subparsers.add_parser("sessions")
    sessions.add_argument("--model")
    sessions.add_argument("--resume")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    if args.lab == "tools":
        exit_code = asyncio.run(tools_sample.run(args.model))
    elif args.lab == "events":
        exit_code = asyncio.run(events_sample.run(args.model))
    elif args.lab == "sessions":
        exit_code = asyncio.run(sessions_sample.run(args.model, args.resume))
    else:
        exit_code = asyncio.run(mcp_sample.run(args.model))
    raise SystemExit(exit_code)


if __name__ == "__main__":
    main()
