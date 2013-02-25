﻿using System;
using Helper;

/*
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

namespace quake
{
    using System.Diagnostics;

    public partial class prog
    {
        static void RETURN_EDICT(edict_t e) { pr_globals_write(OFS_RETURN, EDICT_TO_PROG(e)); }

        /*
        ===============================================================================

						        BUILT-IN FUNCTIONS

        ===============================================================================
        */

        static string @out = StringExtensions.StringOfLength(256);
        static string PF_VarString (int	first)
        {
	        int		i;
        	
	        @out = "";
	        for (i=first ; i<pr_argc ; i++)
	        {
		        @out += G_STRING((OFS_PARM0+i*3));
	        }
	        return @out;
        }

        /*
        =================
        PF_errror

        This is a TERMINAL error, which will kill off the entire server.
        Dumps self.

        error(value)
        =================
        */
        static void PF_error ()
        {
            string  s;
            edict_t ed;

            s = PF_VarString(0);
            console.Con_Printf("======SERVER ERROR in " + pr_string(pr_xfunction.s_name) + ":\n" + s + "\n");
            ed = PROG_TO_EDICT(pr_global_struct[0].self);
            //ED_Print(ed);

            host.Host_Error("Program error");
        }

        /*
        =================
        PF_objerror

        Dumps out self, then an error message.  The program is aborted and self is
        removed, but the level can continue.

        objerror(value)
        =================
        */
        static void PF_objerror ()
        {
            string  s;
            edict_t ed;

            s = PF_VarString(0);
            console.Con_Printf("======OBJECT ERROR in " + pr_string(pr_xfunction.s_name) + ":\n" + s + "\n");
            ed = PROG_TO_EDICT(pr_global_struct[0].self);
            //ED_Print(ed);
            ED_Free(ed);

            host.Host_Error("Program error");
        }

        /*
        ==============
        PF_makevectors

        Writes new values for v_forward, v_up, and v_right based on angles
        makevectors(vector)
        ==============
        */
        static void PF_makevectors ()
        {
            mathlib.AngleVectors(G_VECTOR(OFS_PARM0), pr_global_struct[0].v_forward, pr_global_struct[0].v_right, pr_global_struct[0].v_up);
        }

        /*
        =================
        PF_setorigin

        This is the only valid way to move an object without using the physics of the world (setting velocity and waiting).  Directly changing origin will not set internal links correctly, so clipping would be messed up.  This should be called when an object is spawned, and then only if it is teleported.

        setorigin (entity, origin)
        =================
        */
        static void PF_setorigin ()
        {
            edict_t     e;
            double[]    org;

            e = G_EDICT(OFS_PARM0);
            org = G_VECTOR(OFS_PARM1);
            mathlib.VectorCopy(org, e.v.origin);
            //Debug.WriteLine("PF_setorigin {0} {1} {2}", org[0], org[1], org[2]);
            world.SV_LinkEdict(e, false);
        }

        static void SetMinMaxSize (edict_t e, double[] min, double[] max, bool rotate)
        {
	        double[]	angles;
	        double[]	rmin = new double[3], rmax = new double[3];
	        double[][]  bounds = new double[2][];
	        double[]	xvector = new double[2], yvector = new double[2];
	        double	    a;
	        double[]	@base = new double[3], transformed = new double[3];
	        int		    i, j, k, l;

            for (int kk = 0; kk < 2; kk++)
                bounds[kk] = new double[3];

	        for (i=0 ; i<3 ; i++)
		        if (min[i] > max[i])
			        PR_RunError ("backwards mins/maxs");

	        rotate = false;		// FIXME: implement rotation properly again

	        if (!rotate)
	        {
		        mathlib.VectorCopy (min, rmin);
		        mathlib.VectorCopy (max, rmax);
	        }
	        else
	        {
	        // find min / max for rotations
		        angles = e.v.angles;
        		
		        a = angles[1]/180 * mathlib.M_PI;
        		
		        xvector[0] = Math.Cos(a);
		        xvector[1] = Math.Sin(a);
		        yvector[0] = -Math.Sin(a);
		        yvector[1] = Math.Cos(a);
        		
		        mathlib.VectorCopy (min, bounds[0]);
                mathlib.VectorCopy (max, bounds[1]);
        		
		        rmin[0] = rmin[1] = rmin[2] = 9999;
		        rmax[0] = rmax[1] = rmax[2] = -9999;
        		
		        for (i=0 ; i<= 1 ; i++)
		        {
			        @base[0] = bounds[i][0];
			        for (j=0 ; j<= 1 ; j++)
			        {
				        @base[1] = bounds[j][1];
				        for (k=0 ; k<= 1 ; k++)
				        {
					        @base[2] = bounds[k][2];
        					
				        // transform the point
					        transformed[0] = xvector[0]*@base[0] + yvector[0]*@base[1];
					        transformed[1] = xvector[1]*@base[0] + yvector[1]*@base[1];
					        transformed[2] = @base[2];
        					
					        for (l=0 ; l<3 ; l++)
					        {
						        if (transformed[l] < rmin[l])
							        rmin[l] = transformed[l];
						        if (transformed[l] > rmax[l])
							        rmax[l] = transformed[l];
					        }
				        }
			        }
		        }
	        }
        	
        // set derived values
	        mathlib.VectorCopy (rmin, e.v.mins);
            mathlib.VectorCopy (rmax, e.v.maxs);
            mathlib.VectorSubtract (max, min, e.v.size);

           world. SV_LinkEdict(e, false);
        }

        /*
        =================
        PF_setsize

        the size box is rotated by the current angle

        setsize (entity, minvector, maxvector)
        =================
        */
        static void PF_setsize ()
        {
	        edict_t	    e;
	        double[]	min, max;
        	
	        e = G_EDICT(OFS_PARM0);
	        min = G_VECTOR(OFS_PARM1);
	        max = G_VECTOR(OFS_PARM2);
	        SetMinMaxSize (e, min, max, false);
        }

        /*
        =================
        PF_setmodel

        setmodel(entity, model)
        =================
        */
        static void PF_setmodel ()
        {
	        edict_t         e;
	        string          m, check;
	        model.model_t	mod;
	        int		        i;

	        e = G_EDICT(OFS_PARM0);
	        m = G_STRING(OFS_PARM1);

        // check to see if model was properly precached
            for (i = 0, check = server.sv.model_precache[i]; check != null; i++)
            {
                check = server.sv.model_precache[i];
                if (check.CompareTo(m) == 0)
                    break;
            }
        			
	        if (check == null)
		        PR_RunError ("no precache: " + m + "\n");
        		
	        e.v.model = getStringIndex(m) - 15000;
	        e.v.modelindex = i; //SV_ModelIndex (m);

	        mod = server.sv.models[ (int)e.v.modelindex];  // Mod_ForName (m, true);
        	
	        if (mod != null)
		        SetMinMaxSize (e, mod.mins, mod.maxs, true);
	        else
                SetMinMaxSize(e, mathlib.vec3_origin, mathlib.vec3_origin, true);
        }

        /*
        =================
        PF_bprint

        broadcast print to everyone on server

        bprint(value)
        =================
        */
        static void PF_bprint ()
        {
            string s;
            
            s = PF_VarString(0);
            host.SV_BroadcastPrintf(s);
        }

        /*
        =================
        PF_sprint

        single print to a specific client

        sprint(clientent, value)
        =================
        */

        private static void PF_sprint()
        {
            string s;
            server.client_t client;
            int entnum;

            entnum = G_EDICTNUM(OFS_PARM0);
            s = PF_VarString(1);

            if (entnum < 1 || entnum > server.svs.maxclients)
            {
                console.Con_Printf("tried to sprint to a non-client\n");
                return;
            }

            client = server.svs.clients[entnum - 1];

            common.MSG_WriteChar(client.message, net.svc_print);
            common.MSG_WriteString(client.message, s);
        }

        /*
        =================
        PF_centerprint

        single print to a specific client

        centerprint(clientent, value)
        =================
        */
        private static void PF_centerprint()
        {
            string s;
            server.client_t client;
            int entnum;

            entnum = G_EDICTNUM(OFS_PARM0);
            s = PF_VarString(1);

            if (entnum < 1 || entnum > server.svs.maxclients)
            {
                console.Con_Printf("tried to sprint to a non-client\n");
                return;
            }

            client = server.svs.clients[entnum - 1];

            common.MSG_WriteChar(client.message, net.svc_centerprint);
            common.MSG_WriteString(client.message, s);
        }

        /*
        =================
        PF_normalize

        vector normalize(vector)
        =================
        */
        static void PF_normalize ()
        {

            double[] value1;
	        double[]	newvalue= new double[3];
	        double	@new;
	
	        value1 = G_VECTOR(OFS_PARM0);

	        @new = value1[0] * value1[0] + value1[1] * value1[1] + value1[2]*value1[2];
	        @new = Math.Sqrt(@new);
	
	        if (@new == 0)
		        newvalue[0] = newvalue[1] = newvalue[2] = 0;
	        else
	        {
		        @new = 1/@new;
                newvalue[0] = value1[0] * @new;
                newvalue[1] = value1[1] * @new;
                newvalue[2] = value1[2] * @new;
	        }

            var tempVector = new double[3];
            mathlib.VectorCopy (newvalue, tempVector);
            G_VECTOR_WRITE(OFS_RETURN, tempVector);
        }

        /*
        =================
        PF_vlen

        scalar vlen(vector)
        =================
        */
        static void PF_vlen ()
        {
	        double[]	value1;
            double @new;
	
	        value1 = G_VECTOR(OFS_PARM0);

	        @new = value1[0] * value1[0] + value1[1] * value1[1] + value1[2]*value1[2];
	        @new = Math.Sqrt(@new);
	
            pr_globals_write(OFS_RETURN, @new);
        }

        /*
        =================
        PF_vectoyaw

        float vectoyaw(vector)
        =================
        */
        static void PF_vectoyaw ()
        {
            double[] value1;
            double yaw;

            value1 = G_VECTOR(OFS_PARM0);

            if (value1[1] == 0 && value1[0] == 0)
                yaw = 0;
            else
            {
                yaw = (int)(Math.Atan2(value1[1], value1[0]) * 180 / mathlib.M_PI);
                if (yaw < 0)
                    yaw += 360;
            }

            pr_globals_write(OFS_RETURN, yaw);
        }

        /*
        =================
        PF_vectoangles

        vector vectoangles(vector)
        =================
        */
        static void PF_vectoangles ()
        {
            double[] value1;
            double forward;
            double yaw, pitch;

            value1 = prog.G_VECTOR(OFS_PARM0);

            if (value1[1] == 0 && value1[0] == 0)
            {
                yaw = 0;
                if (value1[2] > 0)
                    pitch = 90;
                else
                    pitch = 270;
            }
            else
            {
                yaw = (int)(Math.Atan2(value1[1], value1[0]) * 180 /mathlib. M_PI);
                if (yaw < 0)
                    yaw += 360;

                forward = Math.Sqrt(value1[0] * value1[0] + value1[1] * value1[1]);
                pitch = (int)(Math.Atan2(value1[2], forward) * 180 / mathlib.M_PI);
                if (pitch < 0)
                    pitch += 360;
            }

            pr_globals_write(OFS_RETURN, pitch);
            pr_globals_write(OFS_RETURN + 1, yaw);
            pr_globals_write(OFS_RETURN + 2, 0);
        }

        /*
        =================
        PF_Random

        Returns a number from 0<= num < 1

        random()
        =================
        */
        static void PF_random ()
        {
	        double		num;
        		
	        num = (helper.rand ()&0x7fff) / ((double)0x7fff);
	        pr_globals_write(OFS_RETURN, num);

        }

        /*
        =================
        PF_particle

        particle(origin, color, count)
        =================
        */
        static void PF_particle ()
        {
	        double[]		org, dir;
	        int		        color;
            int             count;
			
	        org = G_VECTOR(OFS_PARM0);
	        dir = G_VECTOR(OFS_PARM1);
	        color = (int)G_FLOAT(OFS_PARM2);
            count = (int)G_FLOAT(OFS_PARM3);
	        server.SV_StartParticle (org, dir, color, count);
        }
        
        /*
        =================
        PF_ambientsound

        =================
        */
        static void PF_ambientsound ()
        {
            string check;
            string samp;
            double[] pos;
            double vol, attenuation;
            int i, soundnum;

            pos = G_VECTOR(OFS_PARM0);
            samp = G_STRING(OFS_PARM1);
            vol = G_FLOAT(OFS_PARM2);
            attenuation = G_FLOAT(OFS_PARM3);

            for (soundnum = 1; soundnum < quakedef.MAX_SOUNDS
                && server.sv.sound_precache[soundnum] != null; soundnum++)
                if (samp == server.sv.sound_precache[soundnum])
                    break;

            if (soundnum == quakedef.MAX_SOUNDS || !(server.sv.sound_precache[soundnum] != null))
            {
                console.Con_Printf("PF_ambientsound no precache: " + samp + "\n");
                return;
            }

            // add an svc_spawnambient command to the level signon packet
            common.MSG_WriteByte(server.sv.signon, net.svc_spawnstaticsound);
            for (i = 0; i < 3; i++)
                common.MSG_WriteCoord(server.sv.signon, pos[i]);

            common.MSG_WriteByte(server.sv.signon, soundnum);

            common.MSG_WriteByte(server.sv.signon,(int)vol * 255);
            common.MSG_WriteByte(server.sv.signon, (int)attenuation * 64);
        }

        /*
        =================
        PF_sound

        Each entity can have eight independant sound sources, like voice,
        weapon, feet, etc.

        Channel 0 is an auto-allocate channel, the others override anything
        allready running on that entity/channel pair.

        An attenuation of 0 will play full volume everywhere in the level.
        Larger attenuations will drop off.

        =================
        */
        static void PF_sound()
        {
            string sample;
            int channel;
            edict_t entity;
            int volume;
            double attenuation;

            entity = G_EDICT(OFS_PARM0);
            channel = (int)G_FLOAT(OFS_PARM1);
            sample = G_STRING(OFS_PARM2);
            volume = (int)G_FLOAT(OFS_PARM3) * 255;
            attenuation = G_FLOAT(OFS_PARM4);

            if (volume < 0 || volume > 255)
                sys_linux.Sys_Error("SV_StartSound: volume = " + volume);

            if (attenuation < 0 || attenuation > 4)
                sys_linux.Sys_Error("SV_StartSound: attenuation " + attenuation);

            if (channel < 0 || channel > 7)
                sys_linux.Sys_Error("SV_StartSound: channel = " + channel);

            server.SV_StartSound(entity, channel, sample, volume, attenuation);
        }

        /*
        =================
        PF_break

        break()
        =================
        */
        static void PF_break ()
        {
            Debug.WriteLine("PF_break");
        }

        /*
        =================
        PF_traceline

        Used for use tracing and shot targeting
        Traces are blocked by bbox and exact bsp entityes, and also slide box entities
        if the tryents flag is set.

        traceline (vector1, vector2, tryents)
        =================
        */
        static void PF_traceline ()
        {
 	        double[]	v1, v2;
	        world.trace_t	trace;
	        int		nomonsters;
	        edict_t	ent;

	        v1 = G_VECTOR(OFS_PARM0);
	        v2 = G_VECTOR(OFS_PARM1);
	        nomonsters = (int)G_FLOAT(OFS_PARM2);
	        ent = G_EDICT(OFS_PARM3);

            trace = world.SV_Move(v1, mathlib.vec3_origin, mathlib.vec3_origin, v2, nomonsters, ent);

            pr_global_struct[0].trace_allsolid = trace.allsolid ? 1 : 0;
            pr_global_struct[0].trace_startsolid = trace.startsolid ? 1 : 0;
            pr_global_struct[0].trace_fraction = trace.fraction;
            pr_global_struct[0].trace_inwater = trace.inwater ? 1 : 0;
            pr_global_struct[0].trace_inopen = trace.inopen ? 1 : 0;
            mathlib.VectorCopy(trace.endpos, pr_global_struct[0].trace_endpos);
            mathlib.VectorCopy(trace.plane.normal, pr_global_struct[0].trace_plane_normal);
            pr_global_struct[0].trace_plane_dist = trace.plane.dist;	
	        if (trace.ent != null)
                pr_global_struct[0].trace_ent = EDICT_TO_PROG(trace.ent);
	        else
                pr_global_struct[0].trace_ent = EDICT_TO_PROG(server.sv.edicts[0]);
        }

        /*
        =================
        PF_checkpos

        Returns true if the given entity can move to the given position from it's
        current position by walking or rolling.
        FIXME: make work...
        scalar checkpos (entity, vector)
        =================
        */
        static void PF_checkpos ()
        {
            //not implemented in winquake
        }

        //============================================================================
        
        static Uint8Array	checkpvs = new Uint8Array(bspfile.MAX_MAP_LEAFS/8);

        static int PF_newcheckclient (int check)
        {
	        int		i;
	        Uint8Array	pvs;
	        edict_t	ent;
	        model.mleaf_t	leaf;
	        double[]	org = ArrayHelpers.ExplcitDoubleArray(3);

            // cycle to the next one

	        if (check < 1)
		        check = 1;
	        if (check > server.svs.maxclients)
		        check = server.svs.maxclients;

	        if (check == server.svs.maxclients)
		        i = 1;
	        else
		        i = check + 1;

	        for ( ;  ; i++)
	        {
		        if (i == server.svs.maxclients+1)
			        i = 1;

		        ent = EDICT_NUM(i);

		        if (i == check)
			        break;	// didn't find anything else

		        if (ent.free)
			        continue;
		        if (ent.v.health <= 0)
			        continue;
		        if (((int)ent.v.flags & server.FL_NOTARGET) != 0)
			        continue;

	        // anything that is a client, or has a client as an enemy
		        break;
	        }

            // get the PVS for the entity
	        mathlib.VectorAdd (ent.v.origin, ent.v.view_ofs, org);
	        leaf = model.Mod_PointInLeaf (org, server.sv.worldmodel);
	        pvs = model.Mod_LeafPVS (leaf, server.sv.worldmodel);
            Buffer.BlockCopy(pvs, 0, checkpvs, 0, (server.sv.worldmodel.numleafs + 7) >> 3);

	        return i;
        }

        /*
        =================
        PF_checkclient

        Returns a client (or object that has a client enemy) that would be a
        valid target.

        If there are more than one valid options, they are cycled each frame

        If (self.origin + self.viewofs) is not in the PVS of the current target,
        it is not returned at all.

        name checkclient ()
        =================
        */
        static int c_invis, c_notvis;
        static void PF_checkclient()
        {
    	    edict_t	ent, self;
	        model.mleaf_t	leaf;
	        int		l=0;
	        double[]	view = ArrayHelpers.ExplcitDoubleArray(3);
	
        // find a new check if on a new frame
            if (server.sv.time - server.sv.lastchecktime >= 0.1)
	        {
                server.sv.lastcheck = PF_newcheckclient(server.sv.lastcheck);
                server.sv.lastchecktime = server.sv.time;
	        }

        // return check if it might be visible	
            ent = EDICT_NUM(server.sv.lastcheck);
	        if (ent.free || ent.v.health <= 0)
	        {
                RETURN_EDICT(server.sv.edicts[0]);
		        return;
	        }

            // if current entity can't possibly see the check entity, return 0
            self = PROG_TO_EDICT(pr_global_struct[0].self);
            mathlib. VectorAdd (self.v.origin, self.v.view_ofs, view);
            leaf = model.Mod_PointInLeaf(view, server.sv.worldmodel);
            //l = (leaf - server.sv.worldmodel.leafs) - 1;
            for (int i = 0; i < server.sv.worldmodel.leafs.Length; i++)
            {
                var mleafT = server.sv.worldmodel.leafs[i];
                if (mleafT == leaf)  
                {
                    l = i - 1;
                    break;
                }
            }
            Debug.WriteLine("PF_checkclient l: " + l);
            // todo: possible bug, on e1m1 2nd time l = 924, HOWEVER it is 921 in winquake. everything else looks ok tho

            if ( (l<0) || !( (checkpvs[l>>3] & (1<<(l&7))) != 0 ) )
	        {
                c_notvis++;
		        RETURN_EDICT(server.sv.edicts[0]);
                Debug.WriteLine("RETURN_EDICT(server.sv.edicts[0])\n");
                return;
	        }

        // might be able to see it
            c_invis++;
	        RETURN_EDICT(ent);
        }

        //============================================================================

        /*
        =================
        PF_stuffcmd

        Sends text over to the client's execution buffer

        stuffcmd (clientent, value)
        =================
        */

        private static void PF_stuffcmd()
        {
            int entnum;
            string str;
            server.client_t old;

            entnum = G_EDICTNUM(OFS_PARM0);
            if (entnum < 1 || entnum > server.svs.maxclients) PR_RunError("Parm 0 not a client");
            str = G_STRING(OFS_PARM1);

            old = host.host_client;
            host.host_client = server.svs.clients[entnum - 1];
            host.Host_ClientCommands(str);
            host.host_client = old;
        }

        /*
        =================
        PF_localcmd

        Sends text over to the client's execution buffer

        localcmd (string)
        =================
        */
        static void PF_localcmd ()
        {
            string str;

            str = G_STRING(OFS_PARM0);
            cmd.Cbuf_AddText(str);
        }

        /*
        =================
        PF_cvar

        float cvar (string)
        =================
        */
        static void PF_cvar ()
        {
	        string	str;
        	
	        str = G_STRING(OFS_PARM0);
        	
	        pr_globals_write(OFS_RETURN, cvar_t.Cvar_VariableValue (str));
        }

        /*
        =================
        PF_cvar_set

        float cvar (string)
        =================
        */
        static void PF_cvar_set ()
        {
	        string	var, val;
        	
	        var = G_STRING(OFS_PARM0);
	        val = G_STRING(OFS_PARM1);
        	
	        cvar_t.Cvar_Set (var, val);
        }

        /*
        =================
        PF_findradius

        Returns a chain of entities that have origins within a spherical area

        findradius (origin, radius)
        =================
        */
        static void PF_findradius ()
        {
	        edict_t	ent, chain;
	        double	rad;
            double[] org;
            double[] eorg = ArrayHelpers.ExplcitDoubleArray(3);
	        int		i, j;

	        chain = (edict_t )server.sv.edicts[0];
	
	        org = G_VECTOR(OFS_PARM0);
	        rad = G_FLOAT(OFS_PARM1);

	        ent = NEXT_EDICT(server.sv.edicts[0]);
            for (i = 1; i < server.sv.num_edicts; i++, ent = NEXT_EDICT(ent))
	        {
		        if (ent.free)
			        continue;
		        if (ent.v.solid == server.SOLID_NOT)
			        continue;
		        for (j=0 ; j<3 ; j++)
			        eorg[j] = org[j] - (ent.v.origin[j] + (ent.v.mins[j] + ent.v.maxs[j])*0.5);			
		        if (mathlib.Length(eorg) > rad)
			        continue;
			
		        ent.v.chain = EDICT_TO_PROG(chain);
		        chain = ent;
	        }

	        RETURN_EDICT(chain);
        }

        /*
        =========
        PF_dprint
        =========
        */
        static void PF_dprint ()
        {
            try
            {
                console.Con_DPrintf(PF_VarString(0));
            }
            catch
            {
                Debug.WriteLine("todo PF_dprint - crashes");
            }
        }

        private static void PF_ftos()
        {
            double v;
            v = G_FLOAT(OFS_PARM0);

            if (v == (int)v)
            {
                pr_string_temp = ((int)v).ToString();
            }
            else
            {
                pr_string_temp = string.Format("{0:F5}", v);
            }

            //throw new Exception("todo PF_ftos G_INT(OFS_RETURN) = pr_string_temp - pr_strings;");
            var index = getStringIndex(pr_string_temp) - 15000;
            pr_globals_write(OFS_RETURN, index);
            //G_INT(OFS_RETURN) = pr_string_temp - pr_strings; =-- GET INDEX OF STRING AND WRITE TO GLOBALS AS INT opposite of  pr_string() ???? - has that 15000 index thing
        }

        static void PF_fabs ()
        {
            double v;
            v = G_FLOAT(OFS_PARM0);
            pr_globals_write(OFS_RETURN, Math.Abs(v));
        }

        private static string pr_string_temp = null;
        static void PF_vtos ()
        {
            //sprintf (pr_string_temp, "'%5.1f %5.1f %5.1f'", G_VECTOR(OFS_PARM0)[0], G_VECTOR(OFS_PARM0)[1], G_VECTOR(OFS_PARM0)[2]);
            ////G_INT(OFS_RETURN) = pr_string_temp - pr_strings;
            //pr_globals_write(OFS_RETURN, pr_string_temp - pr_strings;); //todo: FIND INDEX OF IT IN ARRAAY AND WRITE TO GLOBAS AS INT? opposite of  pr_string() ???? - has that 15000 index thing

            //throw new Exception("todo TEST PF_vtos;");
            pr_string_temp= string.Format("{0:F5} {1:F5} {2:F5} ", G_VECTOR(OFS_PARM0)[0], G_VECTOR(OFS_PARM0)[1], G_VECTOR(OFS_PARM0)[2]);
            var index = getStringIndex(pr_string_temp) - 15000;
            pr_globals_write(OFS_RETURN, index);
        }

        static void PF_Spawn ()
        {
	        edict_t ed;
	        ed = ED_Alloc();
	        RETURN_EDICT(ed);
        }

        static void PF_Remove ()
        {
	        edict_t	ed;
        	
	        ed = G_EDICT(OFS_PARM0);
	        ED_Free (ed);
        }

        // entity (entity start, .string field, string match) find = #5;
        static void PF_Find ()
        {
	        int		e;	
	        int		f;
	        string	s, t;
	        edict_t	ed;

	        e = G_EDICTNUM(OFS_PARM0);
	        f = G_INT(OFS_PARM1);
	        s = G_STRING(OFS_PARM2);
	        if (s == null)
		        PR_RunError ("PF_Find: bad search string");
        		
	        for (e++ ; e < server.sv.num_edicts ; e++)
	        {
		        ed = EDICT_NUM(e);
		        if (ed.free)
			        continue;
		        t = E_STRING(ed,f);
		        if (t == null)
			        continue;
		        if (t.CompareTo(s) == 0)
		        {
			        RETURN_EDICT(ed);
			        return;
		        }
	        }

	        RETURN_EDICT(server.sv.edicts[0]);
        }

        static void PR_CheckEmptyString(string s)
        {
            if (s[0] <= ' ')
                PR_RunError("Bad string");
        }

        static void PF_precache_file ()
        {
            Debug.WriteLine("PF_precache_file");
            Debug.WriteLine("PF_precache_file");
        }

        static void PF_precache_sound ()
        {
            string  s;
            int     i;

            if (server.sv.state != server.server_state_t.ss_loading)
                PR_RunError("PF_Precache_*: Precache can only be done in spawn functions");

            s = G_STRING(OFS_PARM0);
            pr_globals_write(OFS_RETURN, G_INT(OFS_PARM0));
            PR_CheckEmptyString(s);

            for (i = 0; i < quakedef.MAX_SOUNDS; i++)
            {
                if (server.sv.sound_precache[i] == null)
                {
                    server.sv.sound_precache[i] = s;
                    return;
                }
                if (server.sv.sound_precache[i].CompareTo(s) == 0)
                    return;
            }
            PR_RunError("PF_precache_sound: overflow");
        }

        static void PF_precache_model ()
        {
            string  s;
            int     i;

            if (server.sv.state != server.server_state_t.ss_loading)
                PR_RunError("PF_Precache_*: Precache can only be done in spawn functions");

            s = G_STRING(OFS_PARM0);
            pr_globals_write(OFS_RETURN, G_INT(OFS_PARM0));
            PR_CheckEmptyString(s);

            for (i = 0; i < quakedef.MAX_MODELS; i++)
            {
                if (server.sv.model_precache[i] == null)
                {
                    server.sv.model_precache[i] = s;
                    server.sv.models[i] = model.Mod_ForName(s, true);
                    return;
                }
                if (server.sv.model_precache[i].CompareTo(s) == 0)
                    return;
            }
            PR_RunError("PF_precache_model: overflow");
        }

        static void PF_coredump ()
        {
           prog.ED_PrintEdicts();
        }

        static void PF_traceon ()
        {
            pr_trace = true;
        }

        static void PF_traceoff ()
        {
            pr_trace = false;
        }

        static void PF_eprint ()
        {
            Debug.WriteLine("PF_eprint");
            //ED_PrintNum(G_EDICTNUM(OFS_PARM0));
        }

        /*
        ===============
        PF_walkmove

        float(float yaw, float dist) walkmove
        ===============
        */
        static void PF_walkmove ()
        {
            edict_t ent;
            double yaw, dist;
            double[] move = new double[3];
            dfunction_t oldf;
            int oldself;

            ent = PROG_TO_EDICT(pr_global_struct[0].self);
            yaw = G_FLOAT(OFS_PARM0);
            dist = G_FLOAT(OFS_PARM1);

            if (!    (((int)ent.v.flags & (server.FL_ONGROUND |server. FL_FLY | server.FL_SWIM)) != 0))
            {
                pr_globals_write(OFS_RETURN, 0);
                return;
            }

            yaw = yaw * mathlib.M_PI * 2 / 360;

            move[0] = Math.Cos(yaw) * dist;
            move[1] = Math.Sin(yaw) * dist;
            move[2] = 0;

            // save program state, because SV_movestep may call other progs
            oldf = pr_xfunction;
            oldself = pr_global_struct[0].self;

            pr_globals_write(OFS_RETURN, server.SV_Movestep(ent, move, true) ? 1 : 0);

            // restore program state
            pr_xfunction = oldf;
            pr_global_struct[0].self = oldself;
        }

        /*
        ===============
        PF_droptofloor

        void() droptofloor
        ===============
        */

        private static void PF_droptofloor()
        {
            edict_t ent;
            double[] end = new double[3];
            world.trace_t trace;

            ent = PROG_TO_EDICT(pr_global_struct[0].self);

            mathlib.VectorCopy(ent.v.origin, end);
            end[2] -= 256;

            trace = world.SV_Move(ent.v.origin, ent.v.mins, ent.v.maxs, end, 0, ent);

            if (trace.fraction == 1 || trace.allsolid)
                prog.pr_globals_write(OFS_RETURN, 0); //G_FLOAT(OFS_RETURN) = 0;
            else
            {
                mathlib.VectorCopy(trace.endpos, ent.v.origin);
                world.SV_LinkEdict(ent, false);
                ent.v.flags = (int)ent.v.flags | server.FL_ONGROUND;
                ent.v.groundentity = EDICT_TO_PROG(trace.ent);
                prog.pr_globals_write(OFS_RETURN, 1);//G_FLOAT(OFS_RETURN) = 1;
            }
        }

        /*
        ===============
        PF_lightstyle

        void(float style, string value) lightstyle
        ===============
        */
        static void PF_lightstyle ()
        {
            int             style;
            string          val;
            server.client_t client;
            int             j;

            style = (int)G_FLOAT(OFS_PARM0);
            val = G_STRING(OFS_PARM1);

            // change the string in sv
            server.sv.lightstyles[style] = val;

            // send message to all clients on this server
            if (server.sv.state != server.server_state_t.ss_active)
                return;

            for (j = 0; j < server.svs.maxclients; j++)
            {
                client = server.svs.clients[j];
                if (client.active || client.spawned)
                {
                    common.MSG_WriteChar(client.message, net.svc_lightstyle);
                    common.MSG_WriteChar(client.message, style);
                    common.MSG_WriteString(client.message, val);
                }
            }
        }

        static void PF_rint ()
        {
            double f;
            f = G_FLOAT(OFS_PARM0);
            if (f > 0)
                pr_globals_write(OFS_RETURN, (int)(f + 0.5));
            else
                pr_globals_write(OFS_RETURN, (int)(f - 0.5));
        }
        static void PF_floor ()
        {
            pr_globals_write(OFS_RETURN, Math.Floor(G_FLOAT(OFS_PARM0)));
        }
        static void PF_ceil ()
        {
            pr_globals_write(OFS_RETURN, Math.Ceiling(G_FLOAT(OFS_PARM0)));
        }

        /*
        =============
        PF_checkbottom
        =============
        */
        static void PF_checkbottom ()
        {
            Debug.WriteLine("PF_checkbottom");
            throw  new Exception("PF_checkbottom");
        }

        /*
        =============
        PF_pointcontents
        =============
        */
        static void PF_pointcontents ()
        {
            double[] v;

            v = G_VECTOR(OFS_PARM0);

            pr_globals_write(OFS_RETURN, world.SV_PointContents(v));
        }

        /*
        =============
        PF_nextent

        entity nextent(entity)
        =============
        */
        static void PF_nextent ()
        {
            Debug.WriteLine("PF_nextent");
            throw new Exception("PF_nextent");
        }

        /*
        =============
        PF_aim

        Pick a vector for the player to shoot along
        vector aim(entity, missilespeed)
        =============
        */

        public static cvar_t sv_aim = new cvar_t("sv_aim", "0.93");
        static void PF_aim()
        {
            edict_t	ent, check, bestent;
            double[]	start = new double[3], dir = new double[3], end = new double[3], bestdir = new double[3];
            var tempVector = new double[3];
            int i, j;
            world.trace_t	tr;
            double	dist, bestdist;
            double	speed;
	
            ent = G_EDICT(OFS_PARM0);
            speed = G_FLOAT(OFS_PARM1);

            mathlib.VectorCopy (ent.v.origin, start);
            start[2] += 20;

            // try sending a trace straight
            mathlib.VectorCopy (pr_global_struct[0].v_forward, dir);
            mathlib.VectorMA (start, 2048, dir, end);
            tr = world.SV_Move(start, mathlib.vec3_origin, mathlib.vec3_origin, end, 0, ent);
            if (tr.ent != null && tr.ent.v.takedamage == server.DAMAGE_AIM
            && (!(host.teamplay.value != 0) || ent.v.team <=0 || ent.v.team != tr.ent.v.team) )
            {
                mathlib.VectorCopy(pr_global_struct[0].v_forward, tempVector);
                G_VECTOR_WRITE(OFS_RETURN, tempVector);
	            return;
            }


            // try all possible entities
            mathlib.VectorCopy (dir, bestdir);
            bestdist = sv_aim.value;
            bestent = null;


            //for (i = 0; i < server.sv.num_edicts; i++) //TODO- THIS   IS FASTER CHECK THIS WORKS WHEN FIRING GUN AND ALSO SEE IF IT CAN BE USED ANYWHERE ELSE
            //{
            //    check = server.sv.edicts[i];
            check = NEXT_EDICT(server.sv.edicts[0]);
            for (i=1 ; i<server.sv.num_edicts ; i++, check = NEXT_EDICT(check) )
            {
	            if (check.v.takedamage != server.DAMAGE_AIM)
		            continue;
	            if (check == ent)
		            continue;
	            if (host.teamplay.value != 0 && ent.v.team > 0 && ent.v.team == check.v.team)
		            continue;	// don't aim at teammate
	            for (j=0 ; j<3 ; j++)
		            end[j] = check.v.origin[j]
		            + 0.5*(check.v.mins[j] + check.v.maxs[j]);
	            mathlib.VectorSubtract (end, start, dir);
	            mathlib.VectorNormalize (dir);
                dist = mathlib.DotProduct(dir, pr_global_struct[0].v_forward);
	            if (dist < bestdist)
		            continue;	// to far to turn
                tr = world.SV_Move(start, mathlib.vec3_origin, mathlib.vec3_origin, end, 0, ent);
	            if (tr.ent == check)
	            {	// can shoot at this one
		            bestdist = dist;
		            bestent = check;
	            }
            }
	
            if (bestent != null)
            {
	            mathlib.VectorSubtract (bestent.v.origin, ent.v.origin, dir);
                dist = mathlib.DotProduct(dir, pr_global_struct[0].v_forward);
	            mathlib.VectorScale (pr_global_struct[0].v_forward, dist, end);
	            end[2] = dir[2];
	            mathlib.VectorNormalize (end);
                mathlib.VectorCopy(end, tempVector);
                G_VECTOR_WRITE(OFS_RETURN, tempVector);
            }
            else
            {
                mathlib.VectorCopy(bestdir, tempVector);
                G_VECTOR_WRITE(OFS_RETURN, tempVector);
            }
        }

        /*
        ==============
        PF_changeyaw

        This was a major timewaster in progs, so it was converted to C
        ==============
        */
        public static void PF_changeyaw ()
        {
            edict_t ent;
            double ideal, current, move, speed;

            ent = PROG_TO_EDICT(pr_global_struct[0].self);
            current = mathlib. anglemod(ent.v.angles[1]);
            ideal = ent.v.ideal_yaw;
            speed = ent.v.yaw_speed;

            if (current == ideal)
                return;
            move = ideal - current;
            if (ideal > current)
            {
                if (move >= 180)
                    move = move - 360;
            }
            else
            {
                if (move <= -180)
                    move = move + 360;
            }
            if (move > 0)
            {
                if (move > speed)
                    move = speed;
            }
            else
            {
                if (move < -speed)
                    move = -speed;
            }

            ent.v.angles[1] = mathlib.anglemod(current + move);
        }

        /*
        ===============================================================================

        MESSAGE WRITING

        ===============================================================================
        */
        const int	MSG_BROADCAST	=0;		// unreliable to all
        const int	MSG_ONE			=1;		// reliable to one (msg_entity)
        const int	MSG_ALL			=2;		// reliable to all
        const int   MSG_INIT        = 3;	// write to the init string

        private static common.sizebuf_t WriteDest()
        {
            int entnum;
            int dest;
            edict_t ent;

            dest = (int)G_FLOAT(OFS_PARM0);
            switch (dest)
            {
                case MSG_BROADCAST:
                    return server.sv.datagram;

                case MSG_ONE:
                    ent = PROG_TO_EDICT(pr_global_struct[0].msg_entity);
                    entnum = NUM_FOR_EDICT(ent);
                    if (entnum < 1 || entnum > server.svs.maxclients) 
                        PR_RunError("WriteDest: not a client");
                    return server.svs.clients[entnum - 1].message;

                case MSG_ALL:
                    return server.sv.reliable_datagram;

                case MSG_INIT:
                    return server.sv.signon;

                default:
                    PR_RunError("WriteDest: bad destination");
                    break;
            }

            return null;
        }


        static void PF_WriteByte ()
        {
            var val = G_FLOAT(OFS_PARM1);
            Debug.WriteLine("PF_WriteByte " + val);
            common.MSG_WriteByte(WriteDest(), (int)G_FLOAT(OFS_PARM1));
        }

        static void PF_WriteChar ()
        {
            var val = G_FLOAT(OFS_PARM1);
            Debug.WriteLine("PF_WriteChar " + val);
            common.MSG_WriteChar(WriteDest(), (int)G_FLOAT(OFS_PARM1));
        }

        static void PF_WriteShort ()
        {
            var val = G_FLOAT(OFS_PARM1);
            Debug.WriteLine("PF_WriteShort " + val);
            common.MSG_WriteShort(WriteDest(), (int)G_FLOAT(OFS_PARM1));
        }

        static void PF_WriteLong ()
        {
            var val = G_FLOAT(OFS_PARM1);
            Debug.WriteLine("PF_WriteLong " + val);
            common.MSG_WriteLong(WriteDest(), (int)G_FLOAT(OFS_PARM1));
        }

        static void PF_WriteAngle ()
        {
            var val = G_FLOAT(OFS_PARM1);
            common.MSG_WriteAngle(WriteDest(), (int)G_FLOAT(OFS_PARM1));
        }

        static void PF_WriteCoord ()
        {
            var val = G_FLOAT(OFS_PARM1);
            Debug.WriteLine("PF_WriteCoord " + val);
            common.MSG_WriteCoord(WriteDest(), val);
        }

        static void PF_WriteString ()
        {
            Debug.WriteLine("PF_WriteString");
            throw new Exception("PF_WriteString");
        }
        
        static void PF_WriteEntity ()
        {
            Debug.WriteLine("PF_WriteEntity");
            throw  new Exception("PF_WriteEntity");
        }

        //=============================================================================

        static void PF_makestatic ()
        {
	        edict_t	ent;
	        int		i;
        	
	        ent = G_EDICT(OFS_PARM0);

	        common.MSG_WriteByte (server.sv.signon,net.svc_spawnstatic);

            common.MSG_WriteByte(server.sv.signon, server.SV_ModelIndex(pr_string(ent.v.model)));

            common.MSG_WriteByte(server.sv.signon, (int)ent.v.frame);
            common.MSG_WriteByte(server.sv.signon, (int)ent.v.colormap);
            common.MSG_WriteByte(server.sv.signon, (int)ent.v.skin);
	        for (i=0 ; i<3 ; i++)
	        {
                common.MSG_WriteCoord(server.sv.signon, ent.v.origin[i]);
                common.MSG_WriteAngle(server.sv.signon, ent.v.angles[i]);
	        }

        // throw the entity away now
	        ED_Free (ent);
        }

        //=============================================================================

        /*
        ==============
        PF_setspawnparms
        ==============
        */
        static void PF_setspawnparms ()
        {
            Debug.WriteLine("PF_setspawnparms");
            throw new Exception("PF_setspawnparms");
        }

        /*
        ==============
        PF_changelevel
        ==============
        */
        static void PF_changelevel ()
        {
            string s;

            // make sure we don't issue two changelevels
            if (server.svs.changelevel_issued)
                return;
            server.svs.changelevel_issued = true;

            s = G_STRING(OFS_PARM0);
            cmd.Cbuf_AddText("changelevel " + s + "\n");
        }

        static void PF_Fixme ()
        {
            PR_RunError("unimplemented bulitin");
        }

        static builtin_t[] pr_builtin =
        {
        PF_Fixme,
        PF_makevectors,	// void(entity e)	makevectors 		= #1;
        PF_setorigin,	// void(entity e, vector o) setorigin	= #2;
        PF_setmodel,	// void(entity e, string m) setmodel	= #3;
        PF_setsize,	// void(entity e, vector min, vector max) setsize = #4;
        PF_Fixme,	// void(entity e, vector min, vector max) setabssize = #5;
        PF_break,	// void() break						= #6;
        PF_random,	// float() random						= #7;
        PF_sound,	// void(entity e, float chan, string samp) sound = #8;
        PF_normalize,	// vector(vector v) normalize			= #9;
        PF_error,	// void(string e) error				= #10;
        PF_objerror,	// void(string e) objerror				= #11;
        PF_vlen,	// float(vector v) vlen				= #12;
        PF_vectoyaw,	// float(vector v) vectoyaw		= #13;
        PF_Spawn,	// entity() spawn						= #14;
        PF_Remove,	// void(entity e) remove				= #15;
        PF_traceline,	// float(vector v1, vector v2, float tryents) traceline = #16;
        PF_checkclient,	// entity() clientlist					= #17;
        PF_Find,	// entity(entity start, .string fld, string match) find = #18;
        PF_precache_sound,	// void(string s) precache_sound		= #19;
        PF_precache_model,	// void(string s) precache_model		= #20;
        PF_stuffcmd,	// void(entity client, string s)stuffcmd = #21;
        PF_findradius,	// entity(vector org, float rad) findradius = #22;
        PF_bprint,	// void(string s) bprint				= #23;
        PF_sprint,	// void(entity client, string s) sprint = #24;
        PF_dprint,	// void(string s) dprint				= #25;
        PF_ftos,	// void(string s) ftos				= #26;
        PF_vtos,	// void(string s) vtos				= #27;
        PF_coredump,
        PF_traceon,
        PF_traceoff,
        PF_eprint,	// void(entity e) debug print an entire entity
        PF_walkmove, // float(float yaw, float dist) walkmove
        PF_Fixme, // float(float yaw, float dist) walkmove
        PF_droptofloor,
        PF_lightstyle,
        PF_rint,
        PF_floor,
        PF_ceil,
        PF_Fixme,
        PF_checkbottom,
        PF_pointcontents,
        PF_Fixme,
        PF_fabs,
        PF_aim,
        PF_cvar,
        PF_localcmd,
        PF_nextent,
        PF_particle,
        PF_changeyaw,
        PF_Fixme,
        PF_vectoangles,

        PF_WriteByte,
        PF_WriteChar,
        PF_WriteShort,
        PF_WriteLong,
        PF_WriteCoord,
        PF_WriteAngle,
        PF_WriteString,
        PF_WriteEntity,

        PF_Fixme,
        PF_Fixme,
        PF_Fixme,
        PF_Fixme,
        PF_Fixme,
        PF_Fixme,
        PF_Fixme,

        server.SV_MoveToGoal,
        PF_precache_file,
        PF_makestatic,

        PF_changelevel,
        PF_Fixme,

        PF_cvar_set,
        PF_centerprint,

        PF_ambientsound,

        PF_precache_model,
        PF_precache_sound,		// precache_sound2 is different only for qcc
        PF_precache_file,

        PF_setspawnparms
        };

        static builtin_t[] pr_builtins = pr_builtin;
        static int pr_numbuiltins = pr_builtin.Length;
    }
}
