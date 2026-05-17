import grpc from 'k6/net/grpc';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 10  },
    { duration: '30s', target: 100 },
    { duration: '30s', target: 500 },
    { duration: '10s', target: 0   },
  ],
  thresholds: {
    'grpc_req_duration': ['p(95)<3000'],
  },
};

const client = new grpc.Client();
client.load(['../grpc-service/proto'], 'sensor.proto');

export default function () {
  client.connect('localhost:50051', { plaintext: true });

  // Scenario A — upis
  const ingest = client.invoke('sensor.SensorService/IngestReading', {
    device_id: 'test-device-1',
    co: 0.004956, humidity: 51.0, light: false,
    lpg: 0.007651, motion: false, smoke: 0.020411, temp: 22.7
  });
  check(ingest, { 'A - status OK': (r) => r && r.status === grpc.StatusOK });

  // Scenario B — selektivno
  const selective = client.invoke('sensor.SensorService/GetSelectiveReadings', {
    device_id: 'b8:27:eb:bf:9d:51',
    limit: 100
  });
  check(selective, { 'B - status OK': (r) => r && r.status === grpc.StatusOK });

  // Scenario C — agregacije
  const agg = client.invoke('sensor.SensorService/GetAggregates', {
    device_id: 'b8:27:eb:bf:9d:51',
    from_ts: '2020-07-12',
    to_ts: '2026-05-17'
  });
  check(agg, { 'C - status OK': (r) => r && r.status === grpc.StatusOK });

  client.close();
  sleep(1);
}