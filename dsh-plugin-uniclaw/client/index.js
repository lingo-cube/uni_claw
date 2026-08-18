/**
 * Directory import entry for the loader: re-export the host-side apply.
 * (Node/tsx resolve a directory import through ./index.js; the package
 * manifest's `main` is not consulted for a bare directory spec.)
 */
export { apply } from './lib/index.js';
