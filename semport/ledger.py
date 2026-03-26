#!/usr/bin/env python3
"""
Utility for managing semport/ledger.tsv

Ensures commits are properly tracked and sorted chronologically by iso8601 timestamp.

The `shortsha` field is always stored as a git *short* hash (7 characters, as from
`git rev-parse --short`), even if a longer SHA is provided on input.
"""

import sys
from datetime import datetime
from pathlib import Path
from typing import List, Tuple


def normalize_shortsha(sha: str) -> str:
    """
    Normalize any commit SHA-like string to the short form we store in the ledger.

    - Trims whitespace
    - If longer than 7 characters, truncates to the first 7
    """
    sha = (sha or "").strip()
    if len(sha) > 7:
        return sha[:7]
    return sha


class LedgerEntry:
    """Represents a single ledger entry."""

    def __init__(self, shortsha: str, iso8601: str, disposition: str):
        # Always enforce short hashes in memory, regardless of input length.
        self.shortsha = normalize_shortsha(shortsha)
        self.iso8601 = iso8601
        self.disposition = disposition

    @property
    def timestamp(self) -> datetime:
        """Parse iso8601 timestamp for sorting."""
        return datetime.fromisoformat(self.iso8601.replace('Z', '+00:00'))

    def to_tsv(self) -> str:
        """Convert to TSV format."""
        return f"{self.shortsha}\t{self.iso8601}\t{self.disposition}"

    @classmethod
    def from_tsv(cls, line: str) -> 'LedgerEntry':
        """Parse from TSV line."""
        parts = line.strip().split('\t')
        if len(parts) != 3:
            raise ValueError(f"Invalid TSV line: {line}")
        return cls(parts[0], parts[1], parts[2])

    def __repr__(self):
        return f"LedgerEntry({self.shortsha}, {self.iso8601}, {self.disposition})"


class Ledger:
    """Manages the semport ledger."""

    HEADER = "shortsha\tiso8601\tdisposition"

    def __init__(self, path: Path = None):
        self.path = path or Path(__file__).parent / "ledger.tsv"
        self.entries: List[LedgerEntry] = []

    def load(self) -> 'Ledger':
        """Load ledger from file."""
        if not self.path.exists():
            return self

        with open(self.path) as f:
            lines = [line.strip() for line in f if line.strip()]

        if not lines:
            return self

        # Skip header
        if lines[0] == self.HEADER:
            lines = lines[1:]

        self.entries = [LedgerEntry.from_tsv(line) for line in lines]
        return self

    def save(self):
        """Save ledger to file, sorted by timestamp."""
        # Sort by timestamp
        self.entries.sort(key=lambda e: e.timestamp)

        with open(self.path, 'w') as f:
            f.write(self.HEADER + '\n')
            for entry in self.entries:
                f.write(entry.to_tsv() + '\n')

    def add(self, shortsha: str, iso8601: str, disposition: str = "new") -> bool:
        """
        Add a new commit to the ledger.
        Returns True if added, False if already exists.
        """
        # Always normalize to a short SHA before adding
        shortsha = normalize_shortsha(shortsha)

        # Check if already exists (by short SHA)
        if any(e.shortsha == shortsha for e in self.entries):
            return False

        self.entries.append(LedgerEntry(shortsha, iso8601, disposition))
        return True

    def update_disposition(self, shortsha: str, disposition: str) -> bool:
        """
        Update the disposition of an existing commit.
        Returns True if updated, False if not found.
        """
        # Normalize input so callers can pass either short or full SHA.
        shortsha = normalize_shortsha(shortsha)

        for entry in self.entries:
            if entry.shortsha == shortsha:
                entry.disposition = disposition
                return True
        return False

    def get_earliest_new(self) -> LedgerEntry | None:
        """Get the chronologically earliest commit with disposition='new'."""
        new_entries = [e for e in self.entries if e.disposition == "new"]
        if not new_entries:
            return None
        return min(new_entries, key=lambda e: e.timestamp)

    def get_by_sha(self, shortsha: str) -> LedgerEntry | None:
        """Get entry by commit SHA."""
        shortsha = normalize_shortsha(shortsha)
        for entry in self.entries:
            if entry.shortsha == shortsha:
                return entry
        return None

    def get_by_disposition(self, disposition: str) -> List[LedgerEntry]:
        """Get all entries with given disposition."""
        return [e for e in self.entries if e.disposition == disposition]

    def count_by_disposition(self) -> dict:
        """Count entries by disposition."""
        counts = {"new": 0, "implemented": 0, "acknowledged": 0}
        for entry in self.entries:
            if entry.disposition in counts:
                counts[entry.disposition] += 1
        return counts


def main():
    """CLI interface."""
    import argparse

    parser = argparse.ArgumentParser(description="Manage semport ledger")
    parser.add_argument('--ledger', type=Path, help="Path to ledger.tsv")

    subparsers = parser.add_subparsers(dest='command', help='Commands')

    # add command
    add_parser = subparsers.add_parser('add', help='Add a new commit')
    add_parser.add_argument('shortsha', help='Short commit SHA')
    add_parser.add_argument('iso8601', help='ISO8601 timestamp')
    add_parser.add_argument('--disposition', default='new', choices=['new', 'implemented', 'acknowledged'])

    # update command
    update_parser = subparsers.add_parser('update', help='Update commit disposition')
    update_parser.add_argument('shortsha', help='Short commit SHA')
    update_parser.add_argument('disposition', choices=['new', 'implemented', 'acknowledged'])

    # sort command
    subparsers.add_parser('sort', help='Sort ledger by timestamp')

    # earliest command
    subparsers.add_parser('earliest', help='Get earliest "new" commit')

    # stats command
    subparsers.add_parser('stats', help='Show ledger statistics')

    args = parser.parse_args()

    if not args.command:
        parser.print_help()
        return 1

    ledger = Ledger(args.ledger).load()

    if args.command == 'add':
        if ledger.add(args.shortsha, args.iso8601, args.disposition):
            ledger.save()
            print(f"Added {args.shortsha} with disposition '{args.disposition}'")
        else:
            print(f"Commit {args.shortsha} already exists", file=sys.stderr)
            return 1

    elif args.command == 'update':
        if ledger.update_disposition(args.shortsha, args.disposition):
            ledger.save()
            print(f"Updated {args.shortsha} to '{args.disposition}'")
        else:
            print(f"Commit {args.shortsha} not found", file=sys.stderr)
            return 1

    elif args.command == 'sort':
        ledger.save()
        print(f"Sorted {len(ledger.entries)} entries by timestamp")

    elif args.command == 'earliest':
        entry = ledger.get_earliest_new()
        if entry:
            print(entry.to_tsv())
        else:
            print("No 'new' commits found", file=sys.stderr)
            return 1

    elif args.command == 'stats':
        counts = ledger.count_by_disposition()
        total = len(ledger.entries)
        print(f"Total commits: {total}")
        print(f"  new:          {counts['new']}")
        print(f"  implemented:  {counts['implemented']}")
        print(f"  acknowledged: {counts['acknowledged']}")

        earliest = ledger.get_earliest_new()
        if earliest:
            print(f"\nNext to process: {earliest.shortsha} ({earliest.iso8601})")

    return 0


if __name__ == '__main__':
    sys.exit(main())
