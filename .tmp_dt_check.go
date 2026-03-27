package main
import (
  "fmt"
  "math"
  types "github.com/cedar-policy/cedar-go/types"
)
func main() {
  vals := []string{
    "-292275055-05-17T16:47:04.192Z",
    "-292275055-05-17T15:47:04.192-0100",
    "-292275055-05-17T17:47:04.192+0100",
    "-292275055-05-17T16:47:04.191Z",
    "-292275055-05-16T16:47:04.192Z",
  }
  for _, s := range vals {
    dt, err := types.ParseDatetime(s)
    fmt.Printf("%s => dt=%#v err=%v\n", s, dt, err)
    if err == nil {
      fmt.Printf("marshal => %s\n", string(dt.MarshalCedar()))
    }
  }
  dt := types.NewDatetimeFromMillis(math.MinInt64)
  fmt.Printf("marshal minint64 = %s\n", string(dt.MarshalCedar()))
}
