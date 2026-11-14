#!/usr/bin/env node
// Drives the game on a hidden Win32 desktop. See tools/e2e/README.md.
//
//   node tools/e2e/e2e.mjs start [--scene res://...tscn] [--resolution 1280x720] [--no-build] [--keep-data]
//   node tools/e2e/e2e.mjs send <cmd> [json | key=value ...]
//   node tools/e2e/e2e.mjs shot <name> [width]
//   node tools/e2e/e2e.mjs run <script.json>
//   node tools/e2e/e2e.mjs status | log [lines] | stop
//
// Every command takes --session <name> (default "default"); each session has its own port, folder and save data.

import { spawn, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import net from 'node:net';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const defaultGodot = 'C:\\Development\\Godot_v4.7.1-stable_mono_win64\\Godot_v4.7.1-stable_mono_win64_console.exe';
const launcherProject = path.join(root, 'tools', 'HiddenDesktop');
const launcherExe = path.join(root, 'reports', 'e2e', 'bin', 'HiddenDesktop.exe');
const readyTimeoutMs = 180_000;
const commandTimeoutMs = 600_000;

export function parseArgs(argv) {
  const options = { session: 'default', positional: [] };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (!arg.startsWith('--')) {
      options.positional.push(arg);
      continue;
    }
    const name = arg.slice(2);
    const flag = ['no-build', 'keep-data'].includes(name);
    options[name] = flag ? true : argv[++i];
  }
  return options;
}

/** Turns `key=value` words into an args object: numbers, booleans and JSON arrays/objects keep their type. */
export function parseCommandArgs(words) {
  if (words.length === 1 && words[0].trim().startsWith('{')) return JSON.parse(words[0]);
  const args = {};
  for (const word of words) {
    const eq = word.indexOf('=');
    if (eq < 1) throw new Error(`expected key=value or one JSON object, got "${word}"`);
    const raw = word.slice(eq + 1);
    let value = raw;
    if (/^-?\d+(\.\d+)?$/.test(raw) || raw === 'true' || raw === 'false' || /^[[{]/.test(raw)) value = JSON.parse(raw);
    args[word.slice(0, eq)] = value;
  }
  return args;
}

const sessionDir = (name) => path.join(root, 'reports', 'e2e', name);
const sessionFile = (name) => path.join(sessionDir(name), 'session.json');

function readSession(name) {
  if (!fs.existsSync(sessionFile(name))) throw new Error(`no session "${name}"; run: node tools/e2e/e2e.mjs start`);
  return JSON.parse(fs.readFileSync(sessionFile(name), 'utf8'));
}

function alive(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

function run(command, args, what) {
  const result = spawnSync(command, args, { cwd: root, windowsHide: true, encoding: 'utf8' });
  if (result.status !== 0) {
    process.stderr.write(`${result.stdout ?? ''}${result.stderr ?? ''}`.split('\n').filter((l) => /error/i.test(l)).slice(0, 20).join('\n'));
    throw new Error(`${what} failed (exit ${result.status})`);
  }
}

function newestMtime(dir) {
  return fs.readdirSync(dir).filter((f) => f.endsWith('.cs') || f.endsWith('.csproj'))
    .reduce((t, f) => Math.max(t, fs.statSync(path.join(dir, f)).mtimeMs), 0);
}

function ensureLauncher() {
  if (fs.existsSync(launcherExe) && fs.statSync(launcherExe).mtimeMs >= newestMtime(launcherProject)) return;
  run('dotnet', ['build', launcherProject, '-c', 'Release', '-o', path.dirname(launcherExe), '-nologo', '-v', 'q'],
    'building the HiddenDesktop launcher');
}

function freePort() {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const { port } = server.address();
      server.close(() => resolve(port));
    });
  });
}

export function request(port, cmd, args = {}, timeoutMs = commandTimeoutMs) {
  return new Promise((resolve, reject) => {
    const socket = net.connect(port, '127.0.0.1');
    let buffer = '';
    const timer = setTimeout(() => {
      socket.destroy();
      reject(new Error(`${cmd} got no reply in ${timeoutMs / 1000} s`));
    }, timeoutMs);
    socket.setEncoding('utf8');
    socket.on('connect', () => socket.write(`${JSON.stringify({ id: 1, cmd, args })}\n`));
    socket.on('data', (chunk) => {
      buffer += chunk;
      const end = buffer.indexOf('\n');
      if (end < 0) return;
      clearTimeout(timer);
      socket.end();
      resolve(JSON.parse(buffer.slice(0, end)));
    });
    socket.on('error', (error) => {
      clearTimeout(timer);
      reject(error);
    });
  });
}

function breaches(session) {
  const log = path.join(session.dir, 'breaches.jsonl');
  return fs.existsSync(log) ? fs.readFileSync(log, 'utf8').trim().split('\n').filter(Boolean) : [];
}

function warnBreaches(session) {
  for (const line of breaches(session)) process.stderr.write(`HIDDEN-DESKTOP BREACH: ${line}\n`);
}

function tail(file, lines) {
  if (!fs.existsSync(file)) return '';
  return fs.readFileSync(file, 'utf8').split('\n').slice(-lines).join('\n');
}

async function start(options) {
  const name = options.session;
  if (fs.existsSync(sessionFile(name)) && alive(readSession(name).launcherPid))
    throw new Error(`session "${name}" is already running; stop it first`);
  const dir = sessionDir(name);
  if (!options['keep-data']) fs.rmSync(path.join(dir, 'appdata'), { recursive: true, force: true });
  fs.mkdirSync(path.join(dir, 'shots'), { recursive: true });
  for (const f of ['godot.log', 'breaches.jsonl', 'launcher.out', 'launcher.err']) fs.rmSync(path.join(dir, f), { force: true });

  ensureLauncher();
  if (!options['no-build']) run('dotnet', ['build', path.join(root, 'CardCleaner.csproj'), '-nologo', '-v', 'q'], 'dotnet build');

  const godot = process.env.GODOT ?? defaultGodot;
  if (!fs.existsSync(godot)) throw new Error(`no Godot at ${godot}; set GODOT to the console exe`);
  const port = await freePort();
  const resolution = options.resolution ?? '1280x720';
  const godotArgs = ['--path', root, '--audio-driver', 'Dummy', '--windowed', '--resolution', resolution,
    '--log-file', path.join(dir, 'godot.log')];
  if (options.scene) godotArgs.push(options.scene);
  godotArgs.push('--', `--automation-port=${port}`);
  const launcherArgs = ['run', '--desktop', `cardcleaner-e2e-${name}`, '--appdata', path.join(dir, 'appdata'),
    '--watch-log', path.join(dir, 'breaches.jsonl'), '--timeout-sec', String(Number(options['timeout-min'] ?? 120) * 60),
    '--', godot, ...godotArgs];

  const out = fs.openSync(path.join(dir, 'launcher.out'), 'w');
  const err = fs.openSync(path.join(dir, 'launcher.err'), 'w');
  const child = spawn(launcherExe, launcherArgs, { cwd: root, detached: true, windowsHide: true, stdio: ['ignore', out, err] });
  child.unref();
  const session = { name, port, launcherPid: child.pid, dir, resolution, scene: options.scene ?? null, started: new Date().toISOString() };
  fs.writeFileSync(sessionFile(name), JSON.stringify(session, null, 2));

  const deadline = Date.now() + readyTimeoutMs;
  while (Date.now() < deadline) {
    if (!alive(child.pid)) {
      throw new Error(`the game exited during startup.\n--- launcher ---\n${tail(path.join(dir, 'launcher.out'), 5)}${tail(path.join(dir, 'launcher.err'), 10)}\n--- godot.log ---\n${tail(path.join(dir, 'godot.log'), 30)}`);
    }
    try {
      const reply = await request(port, 'ping', {}, 2000);
      if (reply.ok) {
        const launched = tail(path.join(dir, 'launcher.out'), 5).split('\n').find((l) => l.includes('"pid"'));
        session.godotPid = launched ? JSON.parse(launched).pid : null;
        fs.writeFileSync(sessionFile(name), JSON.stringify(session, null, 2));
        console.log(JSON.stringify({ started: name, port, scene: reply.result.scene, godotPid: session.godotPid }));
        warnBreaches(session);
        return;
      }
    } catch {
      // not listening yet
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  spawnSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true });
  throw new Error(`no automation reply within ${readyTimeoutMs / 1000} s; godot.log:\n${tail(path.join(dir, 'godot.log'), 30)}`);
}

async function send(session, cmd, args) {
  const reply = await request(session.port, cmd, args);
  console.log(JSON.stringify(reply.ok ? reply.result : { error: reply.error }, null, 2));
  warnBreaches(session);
  if (!reply.ok) process.exitCode = 1;
  return reply;
}

async function stop(session) {
  if (alive(session.launcherPid)) {
    try {
      await request(session.port, 'quit', {}, 5000);
    } catch {
      // already gone or hung; killed below
    }
    const deadline = Date.now() + 15_000;
    while (alive(session.launcherPid) && Date.now() < deadline) await new Promise((r) => setTimeout(r, 250));
    // The launcher holds Godot in a kill-on-close job, so ending it ends Godot too.
    if (alive(session.launcherPid)) spawnSync('taskkill', ['/PID', String(session.launcherPid), '/T', '/F'], { windowsHide: true });
  }
  console.log(tail(path.join(session.dir, 'launcher.out'), 3).trim());
  warnBreaches(session);
  fs.rmSync(sessionFile(session.name), { force: true });
}

async function runScript(session, file) {
  const steps = JSON.parse(fs.readFileSync(file, 'utf8'));
  for (const [i, step] of steps.entries()) {
    const { cmd, ...args } = step;
    if (cmd === 'screenshot' && args.name) {
      args.path = path.join(session.dir, 'shots', `${args.name}.png`);
      delete args.name;
    }
    process.stdout.write(`step ${i + 1}/${steps.length} ${cmd}: `);
    const reply = await send(session, cmd, args);
    if (!reply.ok) return;
  }
}

async function main() {
  const options = parseArgs(process.argv.slice(2));
  const [verb, ...rest] = options.positional;
  switch (verb) {
    case 'start':
      return start(options);
    case 'send':
      return send(readSession(options.session), rest[0], parseCommandArgs(rest.slice(1)));
    case 'shot': {
      const session = readSession(options.session);
      const args = { path: path.join(session.dir, 'shots', `${rest[0] ?? 'shot'}.png`) };
      if (rest[1]) args.width = Number(rest[1]);
      return send(session, 'screenshot', args);
    }
    case 'run':
      return runScript(readSession(options.session), rest[0]);
    case 'status': {
      const session = readSession(options.session);
      return console.log(JSON.stringify({ ...session, running: alive(session.launcherPid), breaches: breaches(session) }, null, 2));
    }
    case 'log':
      return console.log(tail(path.join(readSession(options.session).dir, 'godot.log'), Number(rest[0] ?? 40)));
    case 'stop':
      return stop(readSession(options.session));
    default:
      console.log('usage: node tools/e2e/e2e.mjs start|send|shot|run|status|log|stop (see tools/e2e/README.md)');
      process.exitCode = verb ? 1 : 0;
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
