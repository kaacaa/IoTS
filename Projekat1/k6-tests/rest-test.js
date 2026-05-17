import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 10  },
    { duration: '30s', target: 100 },
    { duration: '30s', target: 500 },
    { duration: '10s', target: 0   },
  ],
  thresholds: {
    http_req_duration: ['p(95)<3000'],
    http_req_failed:   ['rate<0.05'],
  },
};

const BASE = 'http://localhost:3001';

export default function () {
  // Scenario A — upis
  const ingest = http.post(`${BASE}/readings`, JSON.stringify({
    device_id: 'test-device-1',
    co: 0.004956, humidity: 51.0, light: false,
    lpg: 0.007651, motion: false, smoke: 0.020411, temp: 22.7
  }), { headers: { 'Content-Type': 'application/json' } });
  check(ingest, { 'A - status 201': (r) => r.status === 201 });

  // Scenario B — selektivno
  const selective = http.get(`${BASE}/readings/selective?device_id=b8:27:eb:bf:9d:51`);
  check(selective, { 'B - status 200': (r) => r.status === 200 });

  // Scenario C — agregacije
  const agg = http.get(`${BASE}/readings/aggregate?device_id=b8:27:eb:bf:9d:51&from=2020-07-12&to=2026-05-17`);
  check(agg, { 'C - status 200': (r) => r.status === 200 });

  sleep(1);
}