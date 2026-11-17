"""Python 3 stdio Bot. No third-party packages or model API required."""
import json
import sys

for line in sys.stdin:
    try:
        obs = json.loads(line)
        team = obs['team']
        own = [unit for unit in obs['units'] if unit['team'] == team]
        own.sort(key=lambda unit: (-unit['x'] if team == 'red' else unit['x'], unit['id']))
        order = None
        if own and obs['commander']['points'][team] > 0 and obs['tick'] >= obs['commander']['readyAt'][team]:
            front = own[0]
            order = {'type': 'inspire', 'x': front['x'], 'z': front['z']}
        print(json.dumps({'schema': 'frontline-agent@1', 'kind': 'command', 'requestId': obs['requestId'], 'team': team, 'order': order}), flush=True)
    except (ValueError, KeyError, TypeError):
        pass
