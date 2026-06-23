#!/usr/bin/env python3
import argparse
import base64
import csv
import hashlib
import os
import struct
import subprocess
import sys
import uuid
from datetime import datetime, timezone


def identity_v3_password_hash(password):
    salt = os.urandom(16)
    subkey = hashlib.pbkdf2_hmac("sha512", password.encode(), salt, 100_000, 32)

    payload = bytearray()
    payload.append(0x01)
    payload.extend(struct.pack(">I", 2))
    payload.extend(struct.pack(">I", 100_000))
    payload.extend(struct.pack(">I", len(salt)))
    payload.extend(salt)
    payload.extend(subkey)

    return base64.b64encode(payload).decode()


def run_psql(args, sql):
    command = [
        "docker",
        "exec",
        "-i",
        args.container,
        "psql",
        "-v",
        "ON_ERROR_STOP=1",
        "-U",
        args.db_user,
        "-d",
        args.db_name,
    ]

    completed = subprocess.run(
        command,
        input=sql,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False)

    if completed.returncode != 0:
        sys.stderr.write(completed.stdout)
        sys.stderr.write(completed.stderr)
        raise SystemExit(completed.returncode)

    return completed.stdout


def existing_seeded_count(args):
    sql = f"""
select count(*)
from "AspNetUsers"
where "Email" like '{args.prefix}%@{args.domain}';
"""
    output = run_psql(args, sql)
    for line in output.splitlines():
        stripped = line.strip()
        if stripped.isdigit():
            return int(stripped)

    raise RuntimeError(f"Could not parse seeded user count from psql output: {output!r}")


def seed_users(args):
    existing = existing_seeded_count(args)
    missing = max(0, args.count - existing)

    print(f"Target users: {args.count}")
    print(f"Existing seeded users: {existing}")
    print(f"Users to insert: {missing}")

    if missing == 0:
        return

    password_hash = identity_v3_password_hash(args.password)
    now = datetime.now(timezone.utc).isoformat()

    command = [
        "docker",
        "exec",
        "-i",
        args.container,
        "psql",
        "-v",
        "ON_ERROR_STOP=1",
        "-U",
        args.db_user,
        "-d",
        args.db_name,
    ]

    process = subprocess.Popen(
        command,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True)

    assert process.stdin is not None

    process.stdin.write("""
begin;
create temporary table seed_users (
    "Id" uuid not null,
    "FullName" text,
    "Status" text not null,
    "CreatedAt" timestamp with time zone not null,
    "UserName" varchar(256),
    "NormalizedUserName" varchar(256),
    "Email" varchar(256),
    "NormalizedEmail" varchar(256),
    "EmailConfirmed" boolean not null,
    "PasswordHash" text,
    "SecurityStamp" text,
    "ConcurrencyStamp" text,
    "PhoneNumber" text,
    "PhoneNumberConfirmed" boolean not null,
    "TwoFactorEnabled" boolean not null,
    "LockoutEnd" timestamp with time zone,
    "LockoutEnabled" boolean not null,
    "AccessFailedCount" integer not null
) on commit drop;
copy seed_users (
    "Id",
    "FullName",
    "Status",
    "CreatedAt",
    "UserName",
    "NormalizedUserName",
    "Email",
    "NormalizedEmail",
    "EmailConfirmed",
    "PasswordHash",
    "SecurityStamp",
    "ConcurrencyStamp",
    "PhoneNumber",
    "PhoneNumberConfirmed",
    "TwoFactorEnabled",
    "LockoutEnd",
    "LockoutEnabled",
    "AccessFailedCount"
) from stdin with (format csv, delimiter E'\\t', null '\\N');
""")

    writer = csv.writer(process.stdin, delimiter="\t", lineterminator="\n")
    start_index = existing

    for index in range(start_index, args.count):
        email = f"{args.prefix}{index:06d}@{args.domain}"
        normalized = email.upper()

        writer.writerow([
            str(uuid.uuid4()),
            f"Load User {index:06d}",
            "Active",
            now,
            email,
            normalized,
            email,
            normalized,
            "true",
            password_hash,
            uuid.uuid4().hex.upper(),
            str(uuid.uuid4()),
            r"\N",
            "false",
            "false",
            r"\N",
            "true",
            "0",
        ])

    process.stdin.write(r"""\.
insert into "AspNetUsers" (
    "Id",
    "FullName",
    "Status",
    "CreatedAt",
    "UserName",
    "NormalizedUserName",
    "Email",
    "NormalizedEmail",
    "EmailConfirmed",
    "PasswordHash",
    "SecurityStamp",
    "ConcurrencyStamp",
    "PhoneNumber",
    "PhoneNumberConfirmed",
    "TwoFactorEnabled",
    "LockoutEnd",
    "LockoutEnabled",
    "AccessFailedCount"
)
select
    "Id",
    "FullName",
    "Status",
    "CreatedAt",
    "UserName",
    "NormalizedUserName",
    "Email",
    "NormalizedEmail",
    "EmailConfirmed",
    "PasswordHash",
    "SecurityStamp",
    "ConcurrencyStamp",
    "PhoneNumber",
    "PhoneNumberConfirmed",
    "TwoFactorEnabled",
    "LockoutEnd",
    "LockoutEnabled",
    "AccessFailedCount"
from seed_users
on conflict ("NormalizedUserName") do nothing;
commit;
""")
    process.stdin.close()

    assert process.stdout is not None
    assert process.stderr is not None
    stdout = process.stdout.read()
    stderr = process.stderr.read()
    return_code = process.wait()

    if return_code != 0:
        sys.stderr.write(stdout)
        sys.stderr.write(stderr)
        raise SystemExit(return_code)

    print(stdout.strip())


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--count", type=int, default=100_000)
    parser.add_argument("--prefix", default="loaduser")
    parser.add_argument("--domain", default="example.test")
    parser.add_argument("--password", default="Load123!Load123!")
    parser.add_argument("--container", default="authservice-staging-postgres")
    parser.add_argument("--db-user", default="auth_service_user")
    parser.add_argument("--db-name", default="auth_service")
    args = parser.parse_args()

    if args.count < 1:
        raise SystemExit("--count must be greater than zero")

    seed_users(args)


if __name__ == "__main__":
    main()
