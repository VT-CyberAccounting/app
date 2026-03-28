// using System.Threading.Tasks;
//
// public class AudioBufferTurret {
//     private short[] buffer;
//     private int offset;
//     private int size;
//     private StateEvent<short[]> onBufferFull;
//     private Task recordingTask;
//     private float timePeriod;
//
//     public AudioBufferTurret(StateEvent<short[]> targetEvent, float timePeriod)
//     {
//         size = (int)(VoIPInterface.sampleRate * timePeriod);
//         buffer = new short[size];
//         onBufferFull = targetEvent;
//         this.timePeriod = timePeriod;
//     }
//
//     private async void record() {
//         while (true) {
//             if (offset == size - 1) {
//                 onBufferFull.Set(buffer);
//                 offset = 0;
//             }
//             int res = VoIPInterface.read(buffer, offset, size - offset);
//             if (res > 0) {
//                 offset += res;
//             }
//             await Task.Delay((int)(0.25 * timePeriod * 1000));
//         }
//     }
// }