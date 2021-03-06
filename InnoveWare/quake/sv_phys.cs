﻿/*
Copyright (C) 1996-1997 Id Software, Inc.

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  

See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.

*/
// sv_phys.c

namespace quake
{
    using System;
    using System.Diagnostics;

    using Missing;

    public partial class server
    {
        /*


        pushmove objects do not obey gravity, and do not interact with each other or trigger fields, but block normal movement and push normal objects when they move.

        onground is set for toss objects when they come to a complete rest.  it is set for steping or walking objects 

        doors, plats, etc are SOLID_BSP, and MOVETYPE_PUSH
        bonus items are SOLID_TRIGGER touch, and MOVETYPE_TOSS
        corpses are SOLID_NOT and MOVETYPE_TOSS
        crates are SOLID_BBOX and MOVETYPE_TOSS
        walking monsters are SOLID_SLIDEBOX and MOVETYPE_STEP
        flying/floating monsters are SOLID_SLIDEBOX and MOVETYPE_FLY

        solid_edge items only clip against bsp models.

        */

        static cvar_t sv_friction = new cvar_t("sv_friction", "4", false, true);
        static cvar_t sv_stopspeed = new cvar_t("sv_stopspeed", "100");
        public static cvar_t sv_gravity = new cvar_t("sv_gravity", "800", false, true);
        static cvar_t sv_maxvelocity = new cvar_t("sv_maxvelocity","2000");
        static cvar_t sv_nostep = new cvar_t("sv_nostep", "0");

        public const double	MOVE_EPSILON	= 0.01;

        /*
        ================
        SV_CheckAllEnts
        ================
        */
        public static void SV_CheckAllEnts ()
        {
   	        int			e;
	        prog.edict_t		check;

        // see if any solid entities are inside the final position
	        check = prog.NEXT_EDICT(sv.edicts[0]);
            for (e = 1; e < sv.num_edicts; e++, check = prog.NEXT_EDICT(check))
	        {
		        if (check.free)
			        continue;
		        if (check.v.movetype == MOVETYPE_PUSH
		        || check.v.movetype == MOVETYPE_NONE
        //#ifdef QUAKE2
        //        || check.v.movetype == MOVETYPE_FOLLOW
        //#endif
		        || check.v.movetype == MOVETYPE_NOCLIP)
			        continue;

		        if (world.SV_TestEntityPosition (check) != null)
			        console.Con_Printf ("entity in invalid position\n");
	        }
        }

        /*
        ================
        SV_CheckVelocity
        ================
        */
        static void SV_CheckVelocity (prog.edict_t ent)
        {
	        int		i;

        //
        // bound velocity
        //
	        for (i=0 ; i<3 ; i++)
	        {
		        if (double.IsNaN(ent.v.velocity[i]))
		        {
			        console.Con_Printf ("Got a NaN velocity on " + prog.pr_string(ent.v.classname) + "\n");
			        ent.v.velocity[i] = 0;
		        }
		        if (double.IsNaN(ent.v.origin[i]))
		        {
                    console.Con_Printf("Got a NaN origin on " + prog.pr_string(ent.v.classname) + "\n");
			        ent.v.origin[i] = 0;
		        }
		        if (ent.v.velocity[i] > sv_maxvelocity.value)
			        ent.v.velocity[i] = sv_maxvelocity.value;
		        else if (ent.v.velocity[i] < -sv_maxvelocity.value)
			        ent.v.velocity[i] = -sv_maxvelocity.value;
	        }
        }

        /*
        =============
        SV_RunThink

        Runs thinking code if time.  There is some play in the exact time the think
        function will be called, because it is called before any movement is done
        in a frame.  Not used for pushmove objects, because they must be exact.
        Returns false if the entity removed itself.
        =============
        */
        static bool SV_RunThink(prog.edict_t ent)
        {
            //Debug.WriteLine("SV_RunThink");
            float thinktime;

            thinktime = (float)ent.v.nextthink;
            /*not ">" like ordiginal to fix rounding difference, mainly for debugging*/
            //if (thinktime <= 0 || thinktime > sv.time + host_frametime)
            if (thinktime <= 0 || thinktime >= sv.time + host.host_frametime)
                return true;

            if (thinktime < sv.time)
                thinktime = (float)sv.time;	// don't let things stay in the past.
            // it is possible to start that way
            // by a trigger with a local time.
            ent.v.nextthink = 0;
            prog.pr_global_struct[0].time = thinktime;
            prog.pr_global_struct[0].self = prog.EDICT_TO_PROG(ent);
            prog.pr_global_struct[0].other = prog.EDICT_TO_PROG(sv.edicts[0]);
            prog.PR_ExecuteProgram(prog.pr_functions[ent.v.think]);
            return !ent.free;
        }
        
/*
==================
SV_Impact

Two entities have touched, so run their touch functions
==================
*/
static void SV_Impact (prog.edict_t e1, prog.edict_t e2)
{
    int old_self, old_other;

    old_self = prog.pr_global_struct[0].self;
    old_other = prog.pr_global_struct[0].other;

    prog.pr_global_struct[0].time = sv.time;
    if (e1.v.touch !=0  && e1.v.solid != SOLID_NOT)
    {
        prog.pr_global_struct[0].self = prog.EDICT_TO_PROG(e1);
        prog.pr_global_struct[0].other = prog.EDICT_TO_PROG(e2);
        prog.PR_ExecuteProgram(prog.pr_functions[e1.v.touch]);
    }

    if (e2.v.touch !=0 && e2.v.solid != SOLID_NOT)
    {
        prog.pr_global_struct[0].self = prog.EDICT_TO_PROG(e2);
        prog.pr_global_struct[0].other = prog.EDICT_TO_PROG(e1);
        prog.PR_ExecuteProgram(prog.pr_functions[e2.v.touch]);
    }

    prog.pr_global_struct[0].self = old_self;
    prog.pr_global_struct[0].other = old_other;
}


/*
==================
ClipVelocity

Slide off of the impacting object
returns the blocked flags (1 = floor, 2 = step / wall)
==================
*/

private const double STOP_EPSILON = 0.1;

static int ClipVelocity (double[] @in, double[] normal, double[] @out, double overbounce)
{
    double backoff;
    double	change;
    int		i, blocked;
	
    blocked = 0;
    if (normal[2] > 0)
        blocked |= 1;		// floor
    if (!(normal[2] != 0))
        blocked |= 2;		// step
	
    backoff = mathlib.DotProduct (@in, normal) * overbounce;

    for (i=0 ; i<3 ; i++)
    {
        change = normal[i]*backoff;
        @out[i] = @in[i] - change;
        if (@out[i] > -STOP_EPSILON && @out[i] < STOP_EPSILON)
            @out[i] = 0;
    }
	
    return blocked;
}


/*
============
SV_FlyMove

The basic solid body movement clip that slides along multiple planes
Returns the clipflags if the velocity was modified (hit something solid)
1 = floor
2 = wall / step
4 = dead stop
If steptrace is not NULL, the trace of any vertical wall hit will be stored
============
*/

        private const int MAX_CLIP_PLANES = 5;

        private static int SV_FlyMove(prog.edict_t ent, double time /*was float*/, ref world.trace_t steptrace)
        {
            int bumpcount, numbumps;
            double[] dir = new double[3] {0, 0, 0};
            double d;
            int numplanes;
            double[][] planes =
                {
                    ArrayHelpers.ExplcitDoubleArray(3), ArrayHelpers.ExplcitDoubleArray(3),
                    ArrayHelpers.ExplcitDoubleArray(3), ArrayHelpers.ExplcitDoubleArray(3),
                    ArrayHelpers.ExplcitDoubleArray(3)
                };
            double[] primal_velocity = new double[3] {0, 0, 0}, original_velocity = new double[3] {0, 0, 0}, new_velocity = new double[3] {0, 0, 0};
            int i, j;
            world.trace_t trace;
            double[] end = new double[3] {0, 0, 0};
            double time_left;
            int blocked;

            numbumps = 4;

            blocked = 0;
            mathlib.VectorCopy(ent.v.velocity, original_velocity);
            mathlib.VectorCopy(ent.v.velocity, primal_velocity);
            numplanes = 0;

            time_left = time;

            //Debug.WriteLine("SV_FlyMove");
            for (bumpcount = 0; bumpcount < numbumps; bumpcount++)
            {
                if (ent.v.velocity[0] == 0.0 && ent.v.velocity[1] == 0.0 && ent.v.velocity[2] == 0.0) 
                    break;

                for (i = 0; i < 3; i++) 
                    end[i] = ent.v.origin[i] + time_left * ent.v.velocity[i];

                trace = world.SV_Move(ent.v.origin, ent.v.mins, ent.v.maxs, end, 0, ent);

                if (trace.allsolid)
                {
                    //Debug.WriteLine("allsolid");
                    // entity is trapped in another solid
                    mathlib.VectorCopy(mathlib.vec3_origin, ent.v.velocity);
                    return 3;
                }

                //Debug.WriteLine(string.Format("fraction {0}", trace.fraction));
                if (trace.fraction > 0)
                {
                    // actually covered some distance
                    mathlib.VectorCopy(trace.endpos, ent.v.origin);
                    mathlib.VectorCopy(ent.v.velocity, original_velocity);
                    numplanes = 0;
                }

                if (trace.fraction == 1.0) 
                    break; // moved the entire distance

                if (trace.ent == null) 
                    sys_linux.Sys_Error("SV_FlyMove: !trace.ent");

                if (trace.plane.normal[2] > 0.7)
                {
                    //Debug.WriteLine("trace.plane.normal[2] > 0.7");
                    blocked |= 1; // floor
                    if (trace.ent.v.solid == SOLID_BSP)
                    {
                        ent.v.flags = (int)ent.v.flags | FL_ONGROUND;
                        ent.v.groundentity = prog.EDICT_TO_PROG(trace.ent);
                    }
                }
                if (!(trace.plane.normal[2] != 0.0))
                {
                    //Debug.WriteLine("!trace.plane.normal[2]");
                    blocked |= 2; // step
                    if (steptrace != null) 
                        steptrace = trace; // save for player extrafriction
                }

                //
                // run the impact function
                //
                //Debug.WriteLine("SV_Impact");
                SV_Impact(ent, trace.ent);
                if (ent.free) 
                {
                    //Debug.WriteLine("ent.fre");
                    break; // removed by the impact function
                }


                time_left -= time_left * trace.fraction;

                // cliped to another plane
                if (numplanes >= MAX_CLIP_PLANES)
                {
                    //Debug.WriteLine("numplanes >= MAX_CLIP_PLANES");
                    // this shouldn't really happen
                    mathlib.VectorCopy(mathlib.vec3_origin, ent.v.velocity);
                    return 3;
                }

                mathlib.VectorCopy(trace.plane.normal, planes[numplanes]);
                numplanes++;

                //
                // modify original_velocity so it parallels all of the clip planes
                //
                for (i = 0; i < numplanes; i++)
                {
                    ClipVelocity(original_velocity, planes[i], new_velocity, 1);
                    for (j = 0; j < numplanes; j++)
                        if (j != i)
                        {
                            if (mathlib.DotProduct(new_velocity, planes[j]) < 0) 
                                break; // not ok
                        }
                    if (j == numplanes) 
                        break;
                }

                if (i != numplanes)
                {
                    // go along this plane
                    //Debug.WriteLine("i != numplanes");
                    mathlib.VectorCopy(new_velocity, ent.v.velocity);
                }
                else
                {
                    // go along the crease
                    if (numplanes != 2)
                    {
                        //				Con_Printf ("clip velocity, numplanes == %i\n",numplanes);
                        mathlib.VectorCopy(mathlib.vec3_origin, ent.v.velocity);
                        return 7;
                    }
                    mathlib.CrossProduct(planes[0], planes[1], dir);
                    d = mathlib.DotProduct(dir, ent.v.velocity);
                    mathlib.VectorScale(dir, d, ent.v.velocity);
                }

                //
                // if original velocity is against the original velocity, stop dead
                // to avoid tiny occilations in sloping corners
                //
                if (mathlib.DotProduct(ent.v.velocity, primal_velocity) <= 0)
                {
                    //Debug.WriteLine("DotProductstuff");
                    mathlib.VectorCopy(mathlib.vec3_origin, ent.v.velocity);
                    return blocked;
                }
            }

            return blocked;
        }

        /*
        ============
        SV_AddGravity

        ============
        */
        static void SV_AddGravity (prog.edict_t ent)
        {
	        double	ent_gravity;

            /*eval_t	*val;

            val = GetEdictFieldValue(ent, "gravity"); //TODO GetEdictFieldValue and gravity
            if (val && val._float)
                ent_gravity = val._float;
            else*/
            ent_gravity = 1.0;
	        ent.v.velocity[2] -= ent_gravity * sv_gravity.value * host.host_frametime;
        }
        

/*
===============================================================================

PUSHMOVE

===============================================================================
*/

/*
============
SV_PushEntity

Does not change the entities velocity at all
============
*/
static world.trace_t SV_PushEntity (prog.edict_t ent, double[] push)
{
    world.trace_t trace;
    double[] end = new double[3] {0, 0, 0};

    mathlib.VectorAdd(ent.v.origin, push, end);

    if (ent.v.movetype == MOVETYPE_FLYMISSILE)
        trace = world.SV_Move(ent.v.origin, ent.v.mins, ent.v.maxs, end, world.MOVE_MISSILE, ent);
    else if (ent.v.solid == SOLID_TRIGGER || ent.v.solid == SOLID_NOT)
        // only clip against bmodels
        trace = world.SV_Move(ent.v.origin, ent.v.mins, ent.v.maxs, end, world.MOVE_NOMONSTERS, ent);
    else
        trace = world.SV_Move(ent.v.origin, ent.v.mins, ent.v.maxs, end, world.MOVE_NORMAL, ent);

    mathlib.VectorCopy(trace.endpos, ent.v.origin);
    world.SV_LinkEdict(ent, true);

    if (trace.ent != null)
        SV_Impact(ent, trace.ent);

    return trace;
}					


/*
============
SV_PushMove

============
*/
static void SV_PushMove (prog.edict_t pusher, Double movetime)
{
    int			i, e;
    prog.edict_t		check, block;
    double[]		mins = new double[3] {0, 0, 0}, maxs = new double[3] {0, 0, 0}, move = new double[3] {0, 0, 0};
    double[]		entorig = new double[3] {0, 0, 0}, pushorig = new double[3] {0, 0, 0};
    int			num_moved;
    prog.edict_t[] moved_edict = new prog.edict_t[quakedef.MAX_EDICTS];
    double[][]		moved_from = new double[quakedef.MAX_EDICTS][];


    if (pusher.v.velocity[0]==0 && pusher.v.velocity[1]==0 && pusher.v.velocity[2]==0)
    {
        pusher.v.ltime += movetime;
        return;
    }

    for (i=0 ; i<3 ; i++)
    {
        move[i] = pusher.v.velocity[i] * movetime;
        mins[i] = pusher.v.absmin[i] + move[i];
        maxs[i] = pusher.v.absmax[i] + move[i];
    }

    mathlib.VectorCopy (pusher.v.origin, pushorig);
	
// move the pusher to it's final position

    mathlib.VectorAdd (pusher.v.origin, move, pusher.v.origin);
    pusher.v.ltime += movetime;
    world.SV_LinkEdict (pusher, false);


// see if any solid entities are inside the final position
    num_moved = 0;
    check = prog.NEXT_EDICT(sv.edicts[0]);
    for (e=1 ; e<sv.num_edicts ; e++, check = prog.NEXT_EDICT(check))
    {
        if (check.free)
            continue;
        //Debug.WriteLine(string.Format("e: {0} movetype:{1}", e, (int)check.v.movetype));

        if (check.v.movetype == MOVETYPE_PUSH
        || check.v.movetype == MOVETYPE_NONE

        || check.v.movetype == MOVETYPE_NOCLIP)
            continue;

    // if the entity is standing on the pusher, it will definately be moved
        if ( ! ( ((int)check.v.flags & FL_ONGROUND) != 0
        && prog.PROG_TO_EDICT(check.v.groundentity) == pusher) )
        {
            if ( check.v.absmin[0] >= maxs[0]
            || check.v.absmin[1] >= maxs[1] 
            || check.v.absmin[2] >= maxs[2]
            || check.v.absmax[0] <= mins[0]
            || check.v.absmax[1] <= mins[1]
            || check.v.absmax[2] <= mins[2] )
                continue;

        // see if the ent's bbox is inside the pusher's final position
            if (world.SV_TestEntityPosition (check) != null)
                continue;
        }

    // remove the onground flag for non-players
        if (check.v.movetype != MOVETYPE_WALK)
            check.v.flags = (int)check.v.flags & ~FL_ONGROUND;

        for (int j = 0; j < moved_from.Length; j++)
        {
            moved_from[j] = new double[3] {0, 0, 0};
        }

        mathlib.VectorCopy (check.v.origin, entorig);
        mathlib.VectorCopy (check.v.origin, moved_from[num_moved]);
        moved_edict[num_moved] = check;
        num_moved++;

        // try moving the contacted entity 
        pusher.v.solid = SOLID_NOT;
        SV_PushEntity (check, move);
        pusher.v.solid = SOLID_BSP;

    // if it is still inside the pusher, block
        block = world.SV_TestEntityPosition (check);
        if (block != null)
        {	// fail the move
            if (check.v.mins[0] == check.v.maxs[0])
                continue;
            if (check.v.solid == SOLID_NOT || check.v.solid == SOLID_TRIGGER)
            {	// corpse
                check.v.mins[0] = check.v.mins[1] = 0;
                mathlib.VectorCopy (check.v.mins, check.v.maxs);
                continue;
            }
			
            mathlib.VectorCopy (entorig, check.v.origin);
            world.SV_LinkEdict (check, true);

            mathlib.VectorCopy (pushorig, pusher.v.origin);
            world.SV_LinkEdict (pusher, false);
            pusher.v.ltime -= movetime;

            // if the pusher has a "blocked" function, call it
            // otherwise, just stay in place until the obstacle is gone
            if (pusher.v.blocked != null)
            {
                prog.pr_global_struct[0].self = prog.EDICT_TO_PROG(pusher);
               prog.pr_global_struct[0].other = prog.EDICT_TO_PROG(check);
                prog.PR_ExecuteProgram (prog.pr_functions[pusher.v.blocked]);
            }
			
        // move back any entities we already moved
            for (i=0 ; i<num_moved ; i++)
            {
                mathlib.VectorCopy (moved_from[i], moved_edict[i].v.origin);
                world.SV_LinkEdict (moved_edict[i], false);
            }
            return;
        }	
    }

	
}
        /*
        ================
        SV_Physics_Pusher

        ================
        */
        static void SV_Physics_Pusher (prog.edict_t ent)
        {
	        double	thinktime;
	        double	oldltime;
	        double	movetime;

	        oldltime = ent.v.ltime;
        	
	        thinktime = ent.v.nextthink;
	        if (thinktime < ent.v.ltime + host.host_frametime)
	        {
		        movetime = thinktime - ent.v.ltime;
		        if (movetime < 0)
			        movetime = 0;
	        }
	        else
		        movetime = host.host_frametime;

	        if (movetime != 0)
	        {
			    SV_PushMove (ent, movetime);	// advances ent.v.ltime if not blocked
	        }

            if ((float)thinktime > (float)oldltime && (float)thinktime <= (float)ent.v.ltime)
	        {
		        ent.v.nextthink = 0;
		        prog.pr_global_struct[0].time = sv.time;
                prog.pr_global_struct[0].self = prog.EDICT_TO_PROG(ent);
                prog.pr_global_struct[0].other = prog.EDICT_TO_PROG(sv.edicts[0]);
                prog.PR_ExecuteProgram(prog.pr_functions[ent.v.think]);
		        if (ent.free)
			        return;
	        }
        }

    
        /*
        ===============================================================================

        CLIENT MOVEMENT

        ===============================================================================
        */

        /*
        =============
        SV_CheckStuck

        This is a big hack to try and fix the rare case of getting stuck in the world
        clipping hull.
        =============
        */

        private static void SV_CheckStuck(prog.edict_t ent)
        {
            //Debug.WriteLine("SV_CheckStuck");
            int i, j;
            int z;
            double[] org = new double[3] {0, 0, 0};

            if (world.SV_TestEntityPosition(ent) == null)
            {
                mathlib.VectorCopy(ent.v.origin, ent.v.oldorigin);
                return;
            }

            mathlib.VectorCopy(ent.v.origin, org);
            mathlib.VectorCopy(ent.v.oldorigin, ent.v.origin);
            if (world.SV_TestEntityPosition(ent) == null)
            {
                console.Con_DPrintf("Unstuck.\n");
                world.SV_LinkEdict(ent, true);
                return;
            }

            for (z = 0; z < 18; z++)
                for (i = -1; i <= 1; i++)
                    for (j = -1; j <= 1; j++)
                    {
                        ent.v.origin[0] = org[0] + i;
                        ent.v.origin[1] = org[1] + j;
                        ent.v.origin[2] = org[2] + z;
                        if (world.SV_TestEntityPosition(ent) != null)
                        {
                            console.Con_DPrintf("Unstuck.\n");
                            world.SV_LinkEdict(ent, true);
                            return;
                        }
                    }

            mathlib.VectorCopy(org, ent.v.origin);
            console.Con_DPrintf("player is stuck.\n");
        }


        /*
        =============
        SV_CheckWater
        =============
        */
        static bool SV_CheckWater (prog.edict_t ent)
        {
            //Debug.WriteLine("SV_CheckWater");
            double[] point = new double[3] {0, 0, 0};
            int cont;

            point[0] = ent.v.origin[0];
            point[1] = ent.v.origin[1];
            point[2] = ent.v.origin[2] + ent.v.mins[2] + 1;

            ent.v.waterlevel = 0;
            ent.v.watertype = bspfile.CONTENTS_EMPTY;
            cont = world.SV_PointContents(point);
            if (cont <= bspfile.CONTENTS_WATER)
            {
                ent.v.watertype = cont;
                ent.v.waterlevel = 1;
                point[2] = ent.v.origin[2] + (ent.v.mins[2] + ent.v.maxs[2]) * 0.5;
                cont = world.SV_PointContents(point);
                if (cont <= bspfile.CONTENTS_WATER)
                {
                    ent.v.waterlevel = 2;
                    point[2] = ent.v.origin[2] + ent.v.view_ofs[2];
                    cont = world.SV_PointContents(point);
                    if (cont <= bspfile.CONTENTS_WATER)
                        ent.v.waterlevel = 3;
                }
            }

            return ent.v.waterlevel > 1;
        }

        /*
        ============
        SV_WallFriction

        ============
        */
        static void SV_WallFriction (prog.edict_t ent, world.trace_t trace)
        {
            double[] forward = new double[3] {0, 0, 0}, right = new double[3] {0, 0, 0}, up = new double[3] {0, 0, 0};
            double d, i;
            double[] into = new double[3] {0, 0, 0}, side = new double[3] {0, 0, 0};

            mathlib.AngleVectors(ent.v.v_angle, forward, right, up);
            d = mathlib.DotProduct(trace.plane.normal, forward);

            d += 0.5;
            if (d >= 0)
                return;

            // cut the tangential velocity
            i = mathlib.DotProduct(trace.plane.normal, ent.v.velocity);
            mathlib.VectorScale(trace.plane.normal, i, into);
            mathlib.VectorSubtract(ent.v.velocity, into, side);

            ent.v.velocity[0] = side[0] * (1 + d);
            ent.v.velocity[1] = side[1] * (1 + d);
        }

        /*
        =====================
        SV_TryUnstick

        Player has come to a dead stop, possibly due to the problem with limited
        float precision at some angle joins in the BSP hull.

        Try fixing by pushing one pixel in each direction.

        This is a hack, but in the interest of good gameplay...
        ======================
        */
        static int SV_TryUnstick (prog.edict_t ent, double[] oldvel)
        {
	        int		i;
	        double[]	oldorg = new double[3] {0, 0, 0};
	        double[]	dir = new double[3] {0, 0, 0};
	        int		clip;
	        world.trace_t	steptrace = new world.trace_t();
	
	        mathlib.VectorCopy (ent.v.origin, oldorg);
	        mathlib.VectorCopy (mathlib.vec3_origin, dir);

	        for (i=0 ; i<8 ; i++)
	        {
        // try pushing a little in an axial direction
		        switch (i)
		        {
			        case 0:	dir[0] = 2; dir[1] = 0; break;
			        case 1:	dir[0] = 0; dir[1] = 2; break;
			        case 2:	dir[0] = -2; dir[1] = 0; break;
			        case 3:	dir[0] = 0; dir[1] = -2; break;
			        case 4:	dir[0] = 2; dir[1] = 2; break;
			        case 5:	dir[0] = -2; dir[1] = 2; break;
			        case 6:	dir[0] = 2; dir[1] = -2; break;
			        case 7:	dir[0] = -2; dir[1] = -2; break;
		        }
		
		        SV_PushEntity (ent, dir);

        // retry the original move
		        ent.v.velocity[0] = oldvel[0];
		        ent.v. velocity[1] = oldvel[1];
		        ent.v. velocity[2] = 0;
		        clip = SV_FlyMove (ent, 0.1f, ref steptrace);

		        if (  Math.Abs(oldorg[1] - ent.v.origin[1]) > 4
		        ||  Math.Abs(oldorg[0] - ent.v.origin[0]) > 4 )
		        {
        //Con_DPrintf ("unstuck!\n");
			        return clip;
		        }
			
        // go back to the original pos and try again
		        mathlib.VectorCopy (oldorg, ent.v.origin);
	        }
	
	        mathlib.VectorCopy (mathlib.vec3_origin, ent.v.velocity);
	        return 7;		// still not moving
        }

    
        /*
        =====================
        SV_WalkMove

        Only used by players
        ======================
        */


        public static void SV_WalkMove(prog.edict_t ent)
        {
            double[] upmove = new double[3] {0, 0, 0}, downmove = new double[3] {0, 0, 0};
            double[] oldorg = new double[3] {0, 0, 0}, oldvel = new double[3] {0, 0, 0};
            double[] nosteporg = new double[3] {0, 0, 0}, nostepvel = new double[3] {0, 0, 0};
            int clip;
            int oldonground;
            world.trace_t steptrace = new world.trace_t(), downtrace = new world.trace_t();

            //
            // do a regular slide move unless it looks like you ran into a step
            //
            oldonground = (int)ent.v.flags & FL_ONGROUND;
            ent.v.flags = (int)ent.v.flags & ~FL_ONGROUND;

            mathlib.VectorCopy(ent.v.origin, oldorg);
            mathlib.VectorCopy(ent.v.velocity, oldvel);

            clip = SV_FlyMove(ent, host.host_frametime, ref steptrace);

            if (!((clip & 2) != 0)) 
                return; // move didn't block on a step

            if (!(oldonground != 0) && ent.v.waterlevel == 0) 
                return; // don't stair up while jumping

            if (ent.v.movetype != MOVETYPE_WALK) 
                return; // gibbed by a trigger

            if (sv_nostep.value != 0.0)
                return;

            if (((int)sv_player.v.flags & FL_WATERJUMP) != 0) 
                return;

            mathlib.VectorCopy(ent.v.origin, nosteporg);
            mathlib.VectorCopy(ent.v.velocity, nostepvel);

            //
            // try moving up and forward to go up a step
            //
            mathlib.VectorCopy(oldorg, ent.v.origin); // back to start pos

            mathlib.VectorCopy(mathlib.vec3_origin, upmove);
            mathlib.VectorCopy(mathlib.vec3_origin, downmove);
            upmove[2] = STEPSIZE;
            downmove[2] = -STEPSIZE + oldvel[2] * host.host_frametime;

            // move up
            SV_PushEntity(ent, upmove); // FIXME: don't link?

            // move forward
            ent.v.velocity[0] = oldvel[0];
            ent.v.velocity[1] = oldvel[1];
            ent.v.velocity[2] = 0;
            clip = SV_FlyMove(ent, host.host_frametime, ref steptrace);

            // check for stuckness, possibly due to the limited precision of floats
            // in the clipping hulls
            if (clip != 0)
            {
                if ( Math.Abs(oldorg[1] - ent.v.origin[1]) < 0.03125 &&  Math.Abs(oldorg[0] - ent.v.origin[0]) < 0.03125)
                {
                    // stepping up didn't make any progress
                    clip = SV_TryUnstick(ent, oldvel);
                }
            }

            // extra friction based on view angle
            if ((clip & 2) != 0) 
                SV_WallFriction(ent, steptrace);

            // move down
            downtrace = SV_PushEntity(ent, downmove); // FIXME: don't link?

            if (downtrace.plane.normal[2] > 0.7)
            {
                if (ent.v.solid == SOLID_BSP)
                {
                    ent.v.flags = (int)ent.v.flags | FL_ONGROUND;
                    ent.v.groundentity = prog.EDICT_TO_PROG(downtrace.ent);
                }
            }
            else
            {
                // if the push down didn't end up on good ground, use the move without
                // the step up.  This happens near wall / slope combinations, and can
                // cause the player to hop up higher on a slope too steep to climb	
                mathlib.VectorCopy(nosteporg, ent.v.origin);
                mathlib.VectorCopy(nostepvel, ent.v.velocity);
            }
        }
        

        /*
        ================
        SV_Physics_Client

        Player character actions
        ================
        */
        static void SV_Physics_Client (prog.edict_t ent, int num)
        {
            //Debug.WriteLine("SV_Physics_Client");
	        if ( ! svs.clients[num-1].active )
		        return;		// unconnected slot

        //
        // call standard client pre-think
        //	
            prog.pr_global_struct[0].time = sv.time;
            prog.pr_global_struct[0].self = prog.EDICT_TO_PROG(ent);
            prog.PR_ExecuteProgram(prog.pr_functions[prog.pr_global_struct[0].PlayerPreThink]);
        	
        //
        // do a move
        //
	        SV_CheckVelocity (ent);

        //
        // decide which move function to call
        //
            switch ((int)ent.v.movetype)
	        {
	        case MOVETYPE_NONE:
		        if (!SV_RunThink (ent))
			        return;
		        break;

	        case MOVETYPE_WALK:
		        if (!SV_RunThink (ent))
			        return;
		        if (!SV_CheckWater (ent) && ! (((int)ent.v.flags & FL_WATERJUMP) != 0) )
			        SV_AddGravity (ent);
                SV_CheckStuck (ent);
		        SV_WalkMove (ent);
		        break;
        		
	        case MOVETYPE_TOSS:
	        case MOVETYPE_BOUNCE:
		        SV_Physics_Toss (ent);
		        break;

	        case MOVETYPE_FLY:
		        if (!SV_RunThink (ent))
			        return;
                var unused_trace_t = new world.trace_t(); // null was passed into SV_FlyMove in original
                SV_FlyMove(ent, host.host_frametime, ref unused_trace_t);
		        break;
        		
	        case MOVETYPE_NOCLIP:
		        if (!SV_RunThink (ent))
			        return;
		        mathlib.VectorMA (ent.v.origin, host.host_frametime, ent.v.velocity, ent.v.origin);
		        break;
        		
	        default:
		        sys_linux.Sys_Error ("SV_Physics_client: bad movetype " + (int)ent.v.movetype);
                break;
	        }

        //
        // call standard player post-think
        //		
           world.SV_LinkEdict (ent, true);

	        prog.pr_global_struct[0].time = sv.time;
            prog.pr_global_struct[0].self = prog.EDICT_TO_PROG(ent);
            prog.PR_ExecuteProgram(prog.pr_functions[prog.pr_global_struct[0].PlayerPostThink]);
        }

        //============================================================================

        /*
        =============
        SV_Physics_None

        Non moving objects can only think
        =============
        */
        static void SV_Physics_None (prog.edict_t ent)
        {
        // regular thinking
	        SV_RunThink (ent);
        }

        /*
        =============
        SV_Physics_Noclip

        A moving object that doesn't obey physics
        =============
        */
        static void SV_Physics_Noclip (prog.edict_t ent)
        {
        // regular thinking
	        if (!SV_RunThink (ent))
		        return;
        	
	        mathlib.VectorMA (ent.v.angles, host.host_frametime, ent.v.avelocity, ent.v.angles);
            mathlib.VectorMA(ent.v.origin, host.host_frametime, ent.v.velocity, ent.v.origin);

            world.SV_LinkEdict(ent, false);
        }
        
        /*
        ==============================================================================

        TOSS / BOUNCE

        ==============================================================================
        */

        /*
        =============
        SV_CheckWaterTransition

        =============
        */
        static void SV_CheckWaterTransition (prog.edict_t ent)
        {
	        int		cont;
	        cont = world.SV_PointContents (ent.v.origin);
	        if (! (ent.v.watertype != 0))
	        {	// just spawned here
		        ent.v.watertype = cont;
		        ent.v.waterlevel = 1;
		        return;
	        }
	
	        if (cont <= bspfile.CONTENTS_WATER)
	        {
                if (ent.v.watertype == bspfile.CONTENTS_EMPTY)
		        {	// just crossed into water
			        SV_StartSound (ent, 0, "misc/h2ohit1.wav", 255, 1);
		        }		
		        ent.v.watertype = cont;
		        ent.v.waterlevel = 1;
	        }
	        else
	        {
                if (ent.v.watertype != bspfile.CONTENTS_EMPTY)
		        {	// just crossed into water
                    SV_StartSound(ent, 0, "misc/h2ohit1.wav", 255, 1);
		        }
                ent.v.watertype = bspfile.CONTENTS_EMPTY;
		        ent.v.waterlevel = cont;
	        }
        }

        /*
        =============
        SV_Physics_Toss

        Toss, bounce, and fly movement.  When onground, do nothing.
        =============
        */
        static void SV_Physics_Toss (prog.edict_t ent)
        {
	        world.trace_t	trace= new world.trace_t();
	        double[]	move = new double[3] {0, 0, 0};
	        double      backoff;

            // regular thinking
	        if (!SV_RunThink (ent))
		        return;

        // if onground, return without moving
	        if ( ((int)ent.v.flags & FL_ONGROUND) != 0 )
		        return;

	        SV_CheckVelocity (ent);

        // add gravity
	        if (ent.v.movetype != MOVETYPE_FLY
	        && ent.v.movetype != MOVETYPE_FLYMISSILE)
		        SV_AddGravity (ent);

        // move angles
	        mathlib.VectorMA (ent.v.angles, host.host_frametime, ent.v.avelocity, ent.v.angles);

        // move origin
	        mathlib.VectorScale (ent.v.velocity, host.host_frametime, move);
            trace = SV_PushEntity (ent, move);
            if (trace.fraction == 1)
                return;
	        if (ent.free)
		        return;
        	
	        if (ent.v.movetype == MOVETYPE_BOUNCE)
		        backoff = 1.5;
	        else
		        backoff = 1;

            ClipVelocity(ent.v.velocity, trace.plane.normal, ent.v.velocity, backoff);

        // stop if on ground
	        if (trace.plane.normal[2] > 0.7)
	        {		
		        if (ent.v.velocity[2] < 60 || ent.v.movetype != MOVETYPE_BOUNCE)
		        {
			        ent.v.flags = (int)ent.v.flags | FL_ONGROUND;
			        ent.v.groundentity = prog.EDICT_TO_PROG(trace.ent);
                    mathlib.VectorCopy(mathlib.vec3_origin, ent.v.velocity);
                    mathlib.VectorCopy(mathlib.vec3_origin, ent.v.avelocity);
		        }
	        }
        	
        // check for in water
            SV_CheckWaterTransition(ent);
        }

        static private void SV_Physics_Step(prog.edict_t ent)
        {
            bool hitsound;

            // freefall if not onground
            if (!(((int)ent.v.flags & (FL_ONGROUND | FL_FLY | FL_SWIM)) != 0))
            {
                if (ent.v.velocity[2] < sv_gravity.value * -0.1) 
                    hitsound = true;
                else 
                    hitsound = false;

                SV_AddGravity(ent);
                SV_CheckVelocity(ent);
                var unused_trace_t = new world.trace_t(); // null was passed into SV_FlyMove in original
                SV_FlyMove(ent, host.host_frametime, ref unused_trace_t);
                world.SV_LinkEdict(ent, true);

                if (((int)ent.v.flags & FL_ONGROUND) != 0) // just hit ground
                {
                    if (hitsound) 
                        SV_StartSound(ent, 0, "demon/dland2.wav", 255, 1);
                }
            }

            // regular thinking
            SV_RunThink(ent);

            SV_CheckWaterTransition(ent);
        }

        /*
        ================
        SV_Physics

        ================
        */

        private static int phys_num = 0;
        public static void SV_Physics ()
        {
	        int		        i;
	        prog.edict_t    ent;

        // let the progs know that a new frame has started
            prog.pr_global_struct[0].self = prog.EDICT_TO_PROG(sv.edicts[0]);
            prog.pr_global_struct[0].other = prog.EDICT_TO_PROG(sv.edicts[0]);
            prog.pr_global_struct[0].time = sv.time;
	        prog.PR_ExecuteProgram (prog.pr_functions[prog.pr_global_struct[0].StartFrame]);

        //SV_CheckAllEnts ();

        //
        // treat each object in turn
        //
	        for (i=0 ; i<sv.num_edicts ; i++)
	        {
                ent = sv.edicts[i];
             
                //if (phys_num >= 2300)
                //    Debug.WriteLine(string.Format("phys_num {0} edict {1} movetype {2} absmin[0] {3}", phys_num, i, (int)ent.v.movetype, (int)ent.v.absmin[0]));
                phys_num++;
                if (ent.free) 
                {
                    //Debug.WriteLine("free");
                    continue;
                }

		        if (prog.pr_global_struct[0].force_retouch != 0)
		        {
                    world.SV_LinkEdict(ent, true);	// force retouch even for stationary
		        }

		        if (i > 0 && i <= svs.maxclients)
			        SV_Physics_Client (ent, i);
		        else if (ent.v.movetype == MOVETYPE_PUSH)
			        SV_Physics_Pusher (ent);
		        else if (ent.v.movetype == MOVETYPE_NONE)
			        SV_Physics_None (ent);
		        else if (ent.v.movetype == MOVETYPE_NOCLIP)
			        SV_Physics_Noclip (ent);
		        else if (ent.v.movetype == MOVETYPE_STEP) {
                    SV_Physics_Step (ent);
                }
		        else if (ent.v.movetype == MOVETYPE_TOSS 
		        || ent.v.movetype == MOVETYPE_BOUNCE
		        || ent.v.movetype == MOVETYPE_FLY
		        || ent.v.movetype == MOVETYPE_FLYMISSILE)
			        SV_Physics_Toss (ent);
		        else
			        sys_linux.Sys_Error ("SV_Physics: bad movetype " + (int)ent.v.movetype);
	        }

	        if (prog.pr_global_struct[0].force_retouch != 0)
		        prog.pr_global_struct[0].force_retouch--;	

	        sv.time += host.host_frametime;
        }
    }
}