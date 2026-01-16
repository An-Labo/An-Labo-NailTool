// tools/update-vpm-json.mjs
// docs/vpm.json の packages.<pkg>.versions に最新バージョンを追記/更新する

import fs from "fs";
import path from "path";

const pkgName = process.env.PACKAGE_NAME || "world.anlabo.mdnailtool";
const repo = process.env.GITHUB_REPOSITORY;
if (!repo) throw new Error("GITHUB_REPOSITORY is not set");

const root = process.cwd();

const packageJsonPath = path.join(root, "Packages", pkgName, "package.json");
const vpmJsonPath = path.join(root, "docs", "vpm.json");

const pkg = JSON.parse(fs.readFileSync(packageJsonPath, "utf8"));
const vpm = JSON.parse(fs.readFileSync(vpmJsonPath, "utf8"));

const version = pkg.version;
if (!version) throw new Error("package.json has no version");

const zipUrl = `https://github.com/${repo}/releases/download/${version}/${pkgName}-${version}.zip`;

const [owner, repoName] = repo.split("/");
const repoUrl = `https://${owner}.github.io/${repoName}/vpm.json`;

vpm.packages ??= {};
vpm.packages[pkgName] ??= { versions: {} };
vpm.packages[pkgName].versions ??= {};

const prev = vpm.packages[pkgName].versions[version] ?? {};

const next = {
  name: pkgName,
  displayName: pkg.displayName ?? prev.displayName ?? pkgName,
  version,
  unity: pkg.unity ?? prev.unity ?? "2022.3",
  description: pkg.description ?? prev.description ?? "",
  author: pkg.author ?? prev.author ?? { name: "An-Labo", email: "", url: "" },
  url: zipUrl,
  repo: repoUrl,
  vpmDependencies: pkg.vpmDependencies ?? prev.vpmDependencies ?? {},
};

vpm.packages[pkgName].versions[version] = next;

fs.writeFileSync(vpmJsonPath, JSON.stringify(vpm, null, 2) + "\n", "utf8");

console.log(`Updated docs/vpm.json: ${pkgName}@${version}`);
console.log(`zip url: ${zipUrl}`);
console.log(`repo url: ${repoUrl}`);
