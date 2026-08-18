/**
 * dsh-plugin-uniclaw-ui — node half.
 *
 * Pure UI plugin: the empty apply exists so the plugin appears in the host
 * cordis.yml / Loader (activation is a no-op — there is no host-side
 * behavior). The browser half ships via exports["./client"], discovered
 * through the package.json dsh.client declaration and served by
 * client-modules as /plugins/dsh-plugin-uniclaw-ui/client.js.
 */
function apply() {}

export { apply };
