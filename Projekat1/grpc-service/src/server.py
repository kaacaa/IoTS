import sys
sys.path.insert(0, '/app/proto_gen')

import grpc
from concurrent import futures
from grpc_reflection.v1alpha import reflection

import sensor_pb2
import sensor_pb2_grpc
from db import get_conn, put_conn

class SensorServicer(sensor_pb2_grpc.SensorServiceServicer):

    # Scenario A — upis
    def IngestReading(self, request, context):
        conn = get_conn()
        try:
            cur = conn.cursor()
            cur.execute(
                """INSERT INTO sensor_readings
                   (ts, device_id, co, humidity, light, lpg, motion, smoke, temp)
                   VALUES (NOW(), %s, %s, %s, %s, %s, %s, %s, %s)
                   RETURNING id, ts""",
                (request.device_id, request.co, request.humidity, request.light,
                 request.lpg, request.motion, request.smoke, request.temp)
            )
            row = cur.fetchone()
            conn.commit()
            return sensor_pb2.ReadingResponse(id=row[0], ts=str(row[1]))
        finally:
            put_conn(conn)

    # Scenario B — selektivno
    def GetSelectiveReadings(self, request, context):
        conn = get_conn()
        try:
            cur = conn.cursor()
            cur.execute(
                """SELECT ts, temp, humidity
                   FROM sensor_readings
                   WHERE device_id = %s
                   ORDER BY ts DESC
                   LIMIT %s""",
                (request.device_id or 'b8:27:eb:bf:9d:51', request.limit or 100)
            )
            rows = cur.fetchall()
            readings = [
                sensor_pb2.SensorReading(
                    ts=str(r[0]),
                    temp=r[1] or 0.0,
                    humidity=r[2] or 0.0
                )
                for r in rows
            ]
            return sensor_pb2.ReadingsListResponse(readings=readings)
        finally:
            put_conn(conn)

    # Scenario C — agregacije
    def GetAggregates(self, request, context):
        conn = get_conn()
        try:
            cur = conn.cursor()
            cur.execute(
                """SELECT
                     DATE_TRUNC('hour', ts),
                     AVG(temp), AVG(humidity),
                     AVG(co), AVG(smoke), COUNT(*)
                   FROM sensor_readings
                   WHERE device_id = %s
                     AND ts >= %s::timestamptz
                     AND ts <= %s::timestamptz
                   GROUP BY DATE_TRUNC('hour', ts)
                   ORDER BY 1 DESC""",
                (
                    request.device_id or 'b8:27:eb:bf:9d:51',
                    request.from_ts   or '2020-07-12',
                    request.to_ts     or '2026-05-17'
                )
            )
            rows = cur.fetchall()
            aggs = [
                sensor_pb2.AggregateData(
                    hour=str(r[0]),
                    avg_temp=r[1] or 0.0,
                    avg_humidity=r[2] or 0.0,
                    avg_co=r[3] or 0.0,
                    avg_smoke=r[4] or 0.0,
                    num_readings=r[5]
                )
                for r in rows
            ]
            return sensor_pb2.AggregateListResponse(aggregates=aggs)
        finally:
            put_conn(conn)

def serve():
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    sensor_pb2_grpc.add_SensorServiceServicer_to_server(SensorServicer(), server)

    # Omoguci reflection da grpcurl moze da cita strukturu
    SERVICE_NAMES = (
        sensor_pb2.DESCRIPTOR.services_by_name['SensorService'].full_name,
        reflection.SERVICE_NAME,
    )
    reflection.enable_server_reflection(SERVICE_NAMES, server)

    server.add_insecure_port('[::]:50051')
    server.start()
    print("gRPC server pokrenut na portu 50051")
    server.wait_for_termination()

if __name__ == '__main__':
    serve()