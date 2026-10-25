const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
require('../core.js');
const Core = globalThis.NotchPrompterCore;

test('Headings, empty scripts and Unicode speaking counts', () => {
  assert.equal(Core.wordCount('# Hook\r\nHello *world*.\n## CTA\nTry this now.'), 5);
  assert.equal(Core.wordCount('# Heading\n***\n'), 0);
  assert.equal(Core.wordCount('తెలుగు స్క్రిప్ట్\nHello there'), 4);
  assert.equal(Core.normalize({script:''}).script, '');
});
test('Invalid persisted values cannot produce runaway pace or unusable geometry', () => {
  const p = Core.normalize({wpm:Infinity, fontSize:NaN, width:-1, height:1e9, countdown:'false', center:false});
  assert.equal(p.wpm, 140); assert.equal(p.fontSize, 28);
  assert.equal(p.width, 320); assert.equal(p.height, 700);
  assert.equal(p.countdown, true); assert.equal(p.center, false);
});
test('Shipping manifest grants temporary page access and bundles executable code locally', () => {
  const manifest = JSON.parse(fs.readFileSync(require('node:path').join(__dirname,'../manifest.json')));
  assert.deepEqual(new Set(manifest.permissions), new Set(['activeTab','storage','scripting']));
  assert.equal(manifest.host_permissions, undefined);
  assert.equal(manifest.content_scripts, undefined);
  assert.ok(manifest.content_security_policy.extension_pages.includes("script-src 'self'"));
});
