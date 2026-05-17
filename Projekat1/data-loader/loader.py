import pandas as pd
import psycopg2
from psycopg2.extras import execute_values
import os
import time

time.sleep(10)

# Proveri da li vec ima podataka
conn_check = psycopg2.connect(
    host=os.getenv("DB_HOST", "postgres"),
    port=os.getenv("DB_PORT", 5432),
    database=os.getenv("DB_NAME", "iotdb"),
    user=os.getenv("DB_USER", "iotuser"),
    password=os.getenv("DB_PASS", "iotpass")
)
cur_check = conn_check.cursor()
cur_check.execute("SELECT COUNT(*) FROM sensor_readings")
count = cur_check.fetchone()[0]
cur_check.close()
conn_check.close()

if count > 0:
    print(f"Baza vec ima {count} redova, preskacemo upis.")
    exit(0)

conn = psycopg2.connect(
    host=os.getenv("DB_HOST", "postgres"),
    port=os.getenv("DB_PORT", 5432),
    database=os.getenv("DB_NAME", "iotdb"),
    user=os.getenv("DB_USER", "iotuser"),
    password=os.getenv("DB_PASS", "iotpass")
)
cur = conn.cursor()

print("Ucitavam CSV...")
df = pd.read_csv("/data/iot_telemetry_data.csv")

# Konvertuj Unix timestamp u datetime
df['ts'] = pd.to_datetime(df['ts'], unit='s', utc=True)
df['device_id'] = df['device']
df['light'] = df['light'].astype(bool)
df['motion'] = df['motion'].astype(bool)

cols = ['ts', 'device_id', 'co', 'humidity', 'light', 'lpg', 'motion', 'smoke', 'temp']
records = [tuple(row) for row in df[cols].itertuples(index=False)]

print(f"Upisujem {len(records)} redova...")
execute_values(
    cur,
    """INSERT INTO sensor_readings
       (ts, device_id, co, humidity, light, lpg, motion, smoke, temp)
       VALUES %s
       ON CONFLICT DO NOTHING""",
    records,
    page_size=1000
)

conn.commit()
cur.close()
conn.close()
print("Gotovo! Baza je napunjena.")